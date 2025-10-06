"""
統合AI株価予測・バックテストパイプライン (GUI版)

本スクリプトは、GUIウィンドウを通して、2つの異なるAI株価予測戦略を実行できる。
各戦略はクラスとして設計されており、容易な拡張が可能。

1. SingleStockStrategy:
   単一銘柄を対象とし、複数モデルを比較・チューニング。
   リスク管理機能に基づき、翌営業日のトレンドを予測する。

2. MultiStockRankingStrategy:
   複数銘柄（TOPIX100）を対象とし、ランキングに基づき、
   翌営業日に売買すべき上位・下位銘柄を推奨する。
"""

# --- ライブラリ ---
import datetime
import os
import sys
import threading
import traceback
import warnings

import japanize_matplotlib
import lightgbm as lgb
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import requests
import talib as ta
import tkinter as tk
import yfinance as yf
from bs4 import BeautifulSoup
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import GridSearchCV
from sklearn.preprocessing import LabelEncoder
from tkinter import ttk, scrolledtext
from tkcalendar import DateEntry

# --- グローバル設定 ---
warnings.filterwarnings('ignore')
pd.set_option('display.max_rows', 100)

# ==============================================================================
# 1. 設定エリア (CONFIG) - デフォルト設定
# ==============================================================================
CONFIG = {
    "common_settings": {
        "start_date": "2021-08-01",
        "end_date": datetime.datetime.now().strftime("%Y-%m-%d"),
        "validation_start_date": "2024-01-01",  # GUIで上書きされるデフォルト値
    },
    "strategy_specific_settings": {
        "SingleStock": {
            "symbol": '7203.T',  # .JPから.Tに変更
            "features": [
                'Close', 'High', 'Low', 'Open', 'Volume', 'SMA5', 'SMA20',
                'MACD', 'RSI', 'Upper', 'Lower', 'OBV', 'ATR', 'DayOfWeek'
            ],
            "models": {
                "RandomForest": RandomForestClassifier(random_state=42),
                "LightGBM": lgb.LGBMClassifier(objective='multiclass', random_state=42, verbosity=-1)
            },
            "param_grids": {
                "RandomForest": {'n_estimators': [100, 200], 'max_depth': [5, 10]},
                "LightGBM": {'n_estimators': [100, 200], 'max_depth': [5, 10], 'learning_rate': [0.1, 0.05]}
            },
            "confidence_threshold": 0.55,
        },
        "MultiStockRanking": {
            "target_variable": "寄り引け変動率",
            "features": [
                '前日比', '寄り引け変動率', '乖離率(25日)', 'S&P500前日比',
                'Nasdaq前日比', 'RSI', 'BB_Width', 'Volume_Ratio'
            ],
            "lgbm_params": {
                "objective": "binary",
                "metric": "auc",
                "learning_rate": 0.01,
                "verbosity": -1,
                "seed": 42,
                "feature_fraction": 0.8,
                "bagging_fraction": 0.8,
                "bagging_freq": 1
            },
            "trading_rule": {"num_rank_trades": 10}
        }
    }
}


# ==============================================================================
# 2. 分析ロジッククラスエリア
# ==============================================================================
class DataLoader:
    def get_topix100_codes(self):
        print("--- TOPIX100 銘柄コードの取得 ---")
        try:
            url = "https://search.sbisec.co.jp/v2/popwin/info/stock/pop690_topix100.html"
            r = requests.get(url, timeout=15)
            r.raise_for_status()
            s = BeautifulSoup(r.content, "html.parser")
            codes = [
                c[0].text.strip() for r in s.select("table tr")
                if (c := r.find_all("td")) and len(c) > 1 and c[0].text.strip().isdigit()
            ]
            if not codes:
                raise ValueError("銘柄コード取得失敗")
            print(f"TOPIX100銘柄数: {len(codes)}")
            return codes
        except Exception as e:
            print(f"X 銘柄コード取得エラー: {e}")
            return []

    def download_market_data(self, symbols, start_date, end_date):
        print(f"\n--- マーケットデータ取得（期間: {start_date} ～ {end_date}）---")
        tickers = [
            s.replace('.JP', '.T') if isinstance(s, str) and s.endswith('.JP')
            else (f"{s}.T" if isinstance(s, int) or s.isdigit() else s)
            for s in symbols
        ]
        
        df = yf.download(
            tickers,
            start=start_date,
            end=end_date,
            auto_adjust=True,
            progress=False
        )
        print("データ取得完了。")
        return df


class FeatureEngineer:
    def calculate_for_single_stock(self, df):
        df['SMA5'] = ta.SMA(df['Close'], 5)
        df['SMA20'] = ta.SMA(df['Close'], 20)
        df['MACD'], _, _ = ta.MACD(df['Close'], 12, 26, 9)
        df['RSI'] = ta.RSI(df['Close'], 14)
        df['Upper'], _, df['Lower'] = ta.BBANDS(df['Close'], 20, 2, 2, 0)
        df['OBV'] = ta.OBV(df['Close'], df['Volume'])
        df['ATR'] = ta.ATR(df['High'], df['Low'], df['Close'], 14)
        df['DayOfWeek'] = df.index.dayofweek
        df['Result'] = df['Close'].shift(-1) - df['Open'].shift(-1)
        df['Correct'] = df['Result'].apply(lambda x: 'UP' if x > 0 else ('DOWN' if x < 0 else 'EVEN'))
        return df

    def calculate_for_multi_stock(self, df):
        print("\n--- 複数銘柄用特徴量 計算 ---")
        df = df.sort_values(['code', 'Date'])
        df['前日比'] = df.groupby('code')['Close'].pct_change(1) * 100
        df['寄り引け変動率'] = (df['Close'] - df['Open']) / df['Open'] * 100
        df['SMA_25'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(25, 25).mean())
        df['乖離率(25日)'] = (df['Close'] - df['SMA_25']) / df['SMA_25'] * 100

        def rsi(s, p=14):
            d = s.diff(1)
            g = d.where(d > 0, 0)
            l = -d.where(d < 0, 0)
            ag = g.rolling(p, p).mean()
            al = l.rolling(p, p).mean()
            rs = ag / al
            return 100 - (100 / (1 + rs))

        df['RSI'] = df.groupby('code')['Close'].transform(lambda x: rsi(x))
        df['SMA_20'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(20).mean())
        df['STD_20'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(20).std())
        df['BB_Width'] = 4 * df['STD_20'] / df['SMA_20']
        df['Volume_SMA_25'] = df.groupby('code')['Volume'].transform(lambda x: x.rolling(25).mean())
        df['Volume_Ratio'] = df['Volume'] / df['Volume_SMA_25']
        return df


class Strategy:
    def __init__(self, common_config, specific_config):
        self.common_config = common_config
        self.specific_config = specific_config
        self.data_loader = DataLoader()
        self.feature_engineer = FeatureEngineer()
        self.df = None
        self.model = None

    def prepare_data(self):
        raise NotImplementedError

    def train_and_evaluate(self):
        raise NotImplementedError

    def display_results(self):
        raise NotImplementedError

    def predict_tomorrow(self):
        raise NotImplementedError

    def run(self):
        self.prepare_data()
        self.train_and_evaluate()
        self.display_results()
        self.predict_tomorrow()


class SingleStockStrategy(Strategy):
    def prepare_data(self):
        print(f"\n===== 単一銘柄戦略 ({self.specific_config['symbol']}) 開始 =====")
        df_raw = self.data_loader.download_market_data(
            [self.specific_config['symbol']],
            self.common_config['start_date'],
            self.common_config['end_date']
        )
        if df_raw.empty:
            print(f"エラー: {self.specific_config['symbol']} データ取得失敗")
            exit()

        if isinstance(df_raw.columns, pd.MultiIndex):
            # MultiIndex の順序を確認して 'Close' がどこにあるか判定
            if 'Close' in df_raw.columns.get_level_values(0):
                df_raw.columns = df_raw.columns.droplevel(1)
            elif 'Close' in df_raw.columns.get_level_values(1):
                df_raw.columns = df_raw.columns.droplevel(0)

        self.df = self.feature_engineer.calculate_for_single_stock(df_raw)
        self.df.dropna(inplace=True)
        self.le = LabelEncoder()
        self.df['Correct_label'] = self.le.fit_transform(self.df['Correct'])
        
        validation_start_dt = pd.to_datetime(self.common_config['validation_start_date'])
        train_df = self.df[self.df.index < validation_start_dt]
        valid_df = self.df[self.df.index >= validation_start_dt]
        
        if train_df.empty:
            print("エラー: 学習データが空です。")
            exit()
            
        self.X_train = train_df[self.specific_config['features']]
        self.y_train = train_df['Correct_label']
        self.X_valid = valid_df[self.specific_config['features']]
        self.y_valid = valid_df['Correct_label']
        self.valid_df = valid_df

    def train_and_evaluate(self):
        results = []
        trained_models = {}
        y_valid_str = self.le.inverse_transform(self.y_valid)

        for name, model in self.specific_config['models'].items():
            print(f"\nモデル '{name}' チューニング/評価中...")
            grid_search = GridSearchCV(
                model,
                self.specific_config['param_grids'][name],
                cv=3,
                scoring='accuracy',
                n_jobs=-1
            )
            grid_search.fit(self.X_train, self.y_train)

            best_model = grid_search.best_estimator_
            trained_models[name] = best_model
            
            predictions = best_model.predict(self.X_valid)
            profit = np.where(
                self.le.inverse_transform(predictions) == y_valid_str,
                abs(self.valid_df['Result']),
                -abs(self.valid_df['Result'])
            ).sum()
            
            results.append({
                "Model": name,
                "Valid_Acc": best_model.score(self.X_valid, self.y_valid),
                "Valid_Profit": profit
            })

        self.results_df = pd.DataFrame(results)
        self.trained_models = trained_models
        self.best_model_name = self.results_df.sort_values("Valid_Profit", ascending=False).iloc[0]["Model"]
        self.model = self.trained_models[self.best_model_name]
        
        print("\n--- モデルチューニング結果 ---")
        print(self.results_df)

    def display_results(self):
        print(f"\n>>> ベストモデル: {self.best_model_name}")
        probabilities = self.model.predict_proba(self.X_valid)
        
        predictions = self.model.predict(self.X_valid)
        final_preds = np.where(
            probabilities.max(axis=1) >= self.specific_config['confidence_threshold'],
            self.le.inverse_transform(predictions),
            'NO_TRADE'
        )
        
        rm_results = self.valid_df[['Result', 'Correct']].copy()
        rm_results['Prediction'] = final_preds

        def calc_profit_rm(row):
            if row['Prediction'] == 'NO_TRADE':
                return 0
            return abs(row['Result']) if row['Prediction'] == row['Correct'] else -abs(row['Result'])

        rm_results['Profit'] = rm_results.apply(calc_profit_rm, axis=1)
        print("\n--- リスク管理後 最終評価 ---")

        original_profit = self.results_df.loc[self.results_df['Model'] == self.best_model_name, 'Valid_Profit'].iloc[0]
        filtered_profit = rm_results['Profit'].sum()
        trade_count = (rm_results['Prediction'] != 'NO_TRADE').sum()
        win_rate = (rm_results['Profit'] > 0).sum() / trade_count if trade_count > 0 else 0
        
        print(f"元の検証利益: {original_profit:.2f} 円\nリスク管理後利益: {filtered_profit:.2f} 円")
        print(f"取引回数: {len(self.X_valid)} -> {trade_count}回\n勝率(取引時): {win_rate:.2%}")

    def predict_tomorrow(self):
        latest_features = self.df[self.specific_config['features']].iloc[-1:]
        tomorrow_prob = self.model.predict_proba(latest_features)[0]
        
        print("\n--- 明日の予測 ---")
        if tomorrow_prob.max() >= self.specific_config['confidence_threshold']:
            pred_index = np.argmax(tomorrow_prob)
            pred_str = self.le.inverse_transform([pred_index])[0]
            print(f"予測: {pred_str} (信頼度: {tomorrow_prob.max():.2%})")
        else:
            print(f"予測: NO_TRADE (信頼度 {tomorrow_prob.max():.2%} がしきい値未満)")


class MultiStockRankingStrategy(Strategy):
    def prepare_data(self):
        print("\n===== 複数銘柄ランキング戦略 開始 =====")
        topix_codes = self.data_loader.get_topix100_codes()
        if not topix_codes:
            exit()

        start_date = self.common_config['start_date']
        end_date = self.common_config['end_date']
        
        jp_data_raw = self.data_loader.download_market_data(topix_codes, start_date, end_date)
        us_data_raw = self.data_loader.download_market_data(["^GSPC", "^IXIC"], start_date, end_date)
        
        jp_data_raw.columns.names = ['feature', 'code']
        df_jp = jp_data_raw.stack(level='code').reset_index()
        df_jp['code'] = df_jp['code'].str.replace('.T', '', regex=False)
        
        df_jp_featured = self.feature_engineer.calculate_for_multi_stock(df_jp)
        
        df_us = pd.DataFrame(index=us_data_raw.index)
        df_us['S&P500_Close'] = us_data_raw.get(('Close', '^GSPC'))
        df_us['Nasdaq_Close'] = us_data_raw.get(('Close', '^IXIC'))
        df_us.reset_index(inplace=True)
        df_us["Date"] = pd.to_datetime(df_us["Date"]).dt.tz_localize(None)
        df_us["S&P500前日比"] = df_us['S&P500_Close'].pct_change() * 100
        df_us["Nasdaq前日比"] = df_us['Nasdaq_Close'].pct_change() * 100
        
        df_merged = pd.merge_asof(
            df_jp_featured.sort_values('Date'),
            df_us[["Date", "S&P500前日比", "Nasdaq前日比"]].dropna(),
            on="Date",
            direction="backward"
        )
        
        df_merged['target_sign'] = (df_merged[self.specific_config["target_variable"]] > 0).astype(int)
        
        for feature in self.specific_config["features"]:
            if feature in df_merged.columns:
                df_merged[f'{feature}_lag1'] = df_merged.groupby('code')[feature].shift(1)
                
        self.final_features = [f'{col}_lag1' for col in self.specific_config["features"]]
        self.target = 'target_sign'
        self.df = df_merged.dropna(subset=self.final_features + [self.target]).copy()

    def train_and_evaluate(self):
        validation_start = pd.to_datetime(self.common_config['validation_start_date'])
        train_idx = self.df["Date"] < validation_start
        valid_idx = self.df["Date"] >= validation_start
        
        df_train = self.df.loc[train_idx]
        df_valid = self.df.loc[valid_idx]
        
        dtrain = lgb.Dataset(df_train[self.final_features], df_train[self.target])
        dvalid = lgb.Dataset(df_valid[self.final_features], df_valid[self.target], reference=dtrain)
        
        print("\n--- LightGBMモデル学習 開始 ---")
        self.model = lgb.train(
            self.specific_config["lgbm_params"],
            dtrain,
            num_boost_round=1000,
            valid_sets=[dtrain, dvalid],
            valid_names=["train", "valid"],
            callbacks=[lgb.early_stopping(stopping_rounds=50, verbose=True)]
        )
        
        valid_pred = self.model.predict(df_valid[self.final_features], num_iteration=self.model.best_iteration)
        df_eval = df_valid.copy()
        df_eval["prediction"] = valid_pred
        
        num_trades = self.specific_config["trading_rule"]["num_rank_trades"]
        df_eval["rank"] = df_eval.groupby("Date")["prediction"].rank(ascending=False)
        df_eval['position'] = 0
        df_eval.loc[df_eval['rank'] <= num_trades, 'position'] = 1
        max_rank = df_eval.groupby('Date')['rank'].transform('max')
        df_eval.loc[df_eval['rank'] > (max_rank - num_trades), 'position'] = -1
        
        self.evaluation_df = df_eval[df_eval['position'] != 0].copy()

    def display_results(self):
        df_importance = pd.DataFrame({
            "特徴量": self.model.feature_name(),
            "重要度": self.model.feature_importance(importance_type="gain")
        }).sort_values("重要度", ascending=False)
        
        print("\n--- 特徴量重要度 ---")
        print(df_importance)
        
        self.evaluation_df["return"] = self.evaluation_df[self.specific_config["target_variable"]] * self.evaluation_df["position"]
        daily_return = self.evaluation_df.groupby("Date")["return"].mean()
        cumulative_return = daily_return.cumsum()
        
        print("\n--- バックテスト パフォーマンス評価 ---")
        if not cumulative_return.empty:
            total_return = cumulative_return.iloc[-1]
            sharpe_ratio = (daily_return.mean() / daily_return.std()) * np.sqrt(252) if daily_return.std() != 0 else 0
            win_rate = (daily_return > 0).sum() / len(daily_return) if len(daily_return) > 0 else 0
            max_drawdown = (cumulative_return.cummax() - cumulative_return).max()

            print(f"最終累積リターン: {total_return:.2f}%")
            print(f"シャープレシオ（年率換算）: {sharpe_ratio:.2f}")
            print(f"勝率: {win_rate:.2%}")
            print(f"最大ドローダウン: {max_drawdown:.2f}%")
            
            plt.figure(figsize=(12, 6))
            cumulative_return.plot()
            plt.title("バックテスト累積リターン（検証期間）")
            plt.xlabel("日付")
            plt.ylabel("累積リターン (%)")
            plt.grid()
            plt.tight_layout()
            
            save_path = "cumulative_return.png"
            plt.savefig(save_path)
            plt.close()  # メモリ解放のためプロットを閉じる
            print(f"\n✅ 累積リターングラフを {save_path} に保存しました。")

    def predict_tomorrow(self):
        print("\n\n" + "=" * 50)
        print("=== 翌営業日の取引銘柄予測を開始 ===")
        print("=" * 50)
        
        d_full_train = lgb.Dataset(self.df[self.final_features], self.df[self.target])
        final_model = lgb.train(
            self.specific_config["lgbm_params"],
            d_full_train,
            num_boost_round=self.model.best_iteration
        )
        
        latest_data = self.df.loc[self.df.groupby('code')['Date'].idxmax()]
        predictions = final_model.predict(latest_data[self.final_features])
        
        df_prediction = pd.DataFrame({'code': latest_data['code'], 'prediction': predictions})
        df_sorted = df_prediction.sort_values('prediction', ascending=False)
        
        num_trades = self.specific_config["trading_rule"]["num_rank_trades"]
        df_buy = df_sorted.head(num_trades)
        df_sell = df_sorted.tail(num_trades).sort_values('prediction', ascending=True)
        
        print("\n--- 予測結果 ---")
        print(f"【買い推奨銘柄】 (上位 {num_trades}銘柄)")
        print(df_buy)
        print(f"\n【売り推奨銘柄】 (下位 {num_trades}銘柄)")
        print(df_sell)


# ==============================================================================
# 3. GUIアプリケーションクラス
# ==============================================================================
class TextRedirector:
    """標準出力をTkinterウィジェットにリダイレクトするクラス。"""
    def __init__(self, widget):
        self.widget = widget

    def write(self, str_val):
        self.widget.configure(state='normal')
        self.widget.insert('end', str_val)
        self.widget.see('end')
        self.widget.configure(state='disabled')

    def flush(self):
        pass


class StockPredGUI:
    def __init__(self, master):
        self.master = master
        master.title("統合AI株価予測パイプライン")
        master.geometry("800x600")

        self._setup_styles()
        self._create_widgets()
        
        self.toggle_controls()
        sys.stdout = TextRedirector(self.output_text)
        sys.stderr = TextRedirector(self.output_text)

    def _setup_styles(self):
        style = ttk.Style()
        style.configure("TFrame", padding=10)
        style.configure("TLabel", padding=5, font=('Yu Gothic UI', 10))
        style.configure("TRadiobutton", padding=5, font=('Yu Gothic UI', 10))
        style.configure("TEntry", padding=5)
        style.configure("TButton", padding=5, font=('Yu Gothic UI', 10, 'bold'))

    def _create_widgets(self):
        main_frame = ttk.Frame(self.master)
        main_frame.pack(fill=tk.BOTH, expand=True)

        control_frame = ttk.Labelframe(main_frame, text="コントロールパネル", padding=10)
        control_frame.pack(fill=tk.X, padx=10, pady=5)

        output_frame = ttk.Labelframe(main_frame, text="実行ログ", padding=10)
        output_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        # --- 戦略選択ウィジェット ---
        self.strategy_var = tk.StringVar(value="SingleStock")
        ttk.Radiobutton(
            control_frame, text="単一銘柄戦略", variable=self.strategy_var,
            value="SingleStock", command=self.toggle_controls
        ).pack(anchor=tk.W)
        
        symbol_frame = ttk.Frame(control_frame)
        symbol_frame.pack(anchor=tk.W, fill=tk.X, padx=20)
        
        self.symbol_label = ttk.Label(symbol_frame, text="銘柄コード (例: 7203.T):")
        self.symbol_label.pack(side=tk.LEFT, padx=5)
        
        self.symbol_entry = ttk.Entry(symbol_frame, width=20)
        self.symbol_entry.insert(0, CONFIG['strategy_specific_settings']['SingleStock']['symbol'])
        self.symbol_entry.pack(side=tk.LEFT)
        
        ttk.Radiobutton(
            control_frame, text="複数銘柄ランキング戦略", variable=self.strategy_var,
            value="MultiStockRanking", command=self.toggle_controls
        ).pack(anchor=tk.W)
        
        # --- 日付選択ウィジェット ---
        date_frame = ttk.Frame(control_frame)
        date_frame.pack(anchor=tk.W, fill=tk.X, pady=5)
        
        self.date_label = ttk.Label(date_frame, text="検証開始日:")
        self.date_label.pack(side=tk.LEFT, padx=(0, 5))
        
        default_date = datetime.datetime.strptime(CONFIG['common_settings']['validation_start_date'], '%Y-%m-%d')
        self.date_entry = DateEntry(
            date_frame, date_pattern='y-mm-dd',
            year=default_date.year, month=default_date.month, day=default_date.day,
            locale='ja_JP'
        )
        self.date_entry.pack(side=tk.LEFT)

        # --- 実行ボタン ---
        self.run_button = ttk.Button(control_frame, text="分析を実行", command=self.run_analysis)
        self.run_button.pack(pady=10)
        
        # --- 出力テキストエリア ---
        self.output_text = scrolledtext.ScrolledText(
            output_frame, state='disabled', wrap=tk.WORD, font=('Courier New', 9)
        )
        self.output_text.pack(fill=tk.BOTH, expand=True)

    def toggle_controls(self):
        """ラジオボタンに応じてUIコントロールの状態を切り替える"""
        is_single_stock = self.strategy_var.get() == "SingleStock"
        state = 'normal' if is_single_stock else 'disabled'
        self.symbol_label.config(state=state)
        self.symbol_entry.config(state=state)

    def run_analysis(self):
        self.run_button.config(state='disabled')
        self.output_text.configure(state='normal')
        self.output_text.delete('1.0', tk.END)
        self.output_text.configure(state='disabled')
        
        analysis_thread = threading.Thread(target=self.analysis_worker)
        analysis_thread.start()

    def analysis_worker(self):
        """バックグラウンドで分析処理を実行する"""
        try:
            strategy_name = self.strategy_var.get()
            
            local_config = CONFIG.copy()
            local_config['common_settings']['validation_start_date'] = self.date_entry.get_date().strftime('%Y-%m-%d')
            
            common_conf = local_config["common_settings"]
            specific_conf = local_config["strategy_specific_settings"][strategy_name]

            if strategy_name == "SingleStock":
                specific_conf['symbol'] = self.symbol_entry.get()
                strategy = SingleStockStrategy(common_conf, specific_conf)
            else:
                strategy = MultiStockRankingStrategy(common_conf, specific_conf)
            
            strategy.run()

        except Exception as e:
            print(f"\n!!!! エラーが発生しました !!!!\n{e}\n\n詳細:\n{traceback.format_exc()}")
        finally:
            self.master.after(0, self.enable_run_button)
            
    def enable_run_button(self):
        self.run_button.config(state='normal')


# ==============================================================================
# 4. アプリケーション起動
# ==============================================================================
if __name__ == '__main__':
    root = tk.Tk()
    app = StockPredGUI(root)
    root.mainloop()