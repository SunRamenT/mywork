# -----------------------------
# ライブラリ
# -----------------------------
import pandas as pd
import datetime
import numpy as np
import os
import warnings

# --- データソース ---
from pandas_datareader import data as pdr

# --- モデル関連 ---
from sklearn.model_selection import GridSearchCV
from sklearn.ensemble import RandomForestClassifier
import lightgbm as lgb
from sklearn.preprocessing import LabelEncoder

# --- 可視化 ---
import matplotlib.pyplot as plt
import seaborn as sns

# --- その他 ---
import talib as ta
warnings.simplefilter('ignore')
plt.rcParams['font.family'] = 'Yu Gothic'

# -----------------------------
# 関数定義セクション
# -----------------------------

def get_stock_data(symbol, start_date, end_date):
    """指定された銘柄の株価データをStooqから取得する"""
    print(f"{symbol}のOHLCVデータをStooqから取得します... (期間: {start_date} ～ {end_date})")
    df = pdr.DataReader(symbol, 'stooq', start_date, end_date)
    return df.sort_index()

def add_technical_features(df):
    """データフレームにテクニカル指標を追加する"""
    df['SMA5'] = ta.SMA(df['Close'], timeperiod=5)
    df['SMA20'] = ta.SMA(df['Close'], timeperiod=20)
    df['MACD'], _, _ = ta.MACD(df['Close'], fastperiod=12, slowperiod=26, signalperiod=9)
    df['RSI'] = ta.RSI(df['Close'], timeperiod=14)
    df['Upper'], _, df['Lower'] = ta.BBANDS(df['Close'], timeperiod=20, nbdevup=2, nbdevdn=2, matype=0)
    df['OBV'] = ta.OBV(df['Close'], df['Volume'])
    df['ATR'] = ta.ATR(df['High'], df['Low'], df['Close'], timeperiod=14)
    df['DayOfWeek'] = df.index.dayofweek
    df['Result'] = df['Close'].shift(-1) - df['Open'].shift(-1)
    df['Correct'] = df['Result'].apply(lambda x: 'UP' if x > 0 else ('DOWN' if x < 0 else 'EVEN'))
    return df

def analyze_feature_importance(model, features):
    """特徴量の重要度を分析し、グラフで表示・保存する"""
    importances = model.feature_importances_
    df_importance = pd.DataFrame({'Feature': features, 'Importance': importances}).sort_values('Importance', ascending=False)
    print("\n--- 特徴量の重要度 ---")
    print(df_importance)
    plt.figure(figsize=(10, 8))
    sns.barplot(x='Importance', y='Feature', data=df_importance)
    plt.title('特徴量の重要度 (Feature Importance)')
    plt.tight_layout()
    output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "feature_importance.png")
    plt.savefig(output_path)
    print(f"\n特徴量の重要度グラフを {output_path} に保存しました。")
    plt.close()

def tune_and_evaluate(X_train, y_train, X_valid, y_valid, models, param_grids):
    """GridSearchCVでモデルをチューニングし、評価する"""
    results, trained_models = [], {}
    global le
    y_valid_str = le.inverse_transform(y_valid)
    for name, model in models.items():
        print(f"\nモデル '{name}' のチューニングと評価を実行中...")
        grid_search = GridSearchCV(model, param_grids[name], cv=3, scoring='accuracy', n_jobs=-1)
        grid_search.fit(X_train, y_train)
        best_model = grid_search.best_estimator_
        trained_models[name] = best_model
        valid_profit = calc_profit(best_model, X_valid, y_valid_str, df.loc[X_valid.index])
        results.append({"Model": name, "Valid_Acc": best_model.score(X_valid, y_valid), "Valid_Profit": valid_profit, "Best_Params": grid_search.best_params_})
    return pd.DataFrame(results), trained_models

def calc_profit(model, X, y_str, df_subset):
    """損益を計算する"""
    pred_labels = model.predict(X)
    pred_str = le.inverse_transform(pred_labels)
    profit_series = pd.Series(np.where(pred_str == y_str, abs(df_subset['Result']), -abs(df_subset['Result'])), index=X.index)
    return profit_series.sum()

def apply_risk_management(model, X, df_subset, threshold):
    """リスク管理を適用して予測と損益を計算"""
    probabilities = model.predict_proba(X)
    max_probs = probabilities.max(axis=1)
    pred_labels = model.predict(X)
    pred_str = le.inverse_transform(pred_labels)
    final_predictions = np.where(max_probs >= threshold, pred_str, 'NO_TRADE')
    def calculate_profit(row):
        if row['Prediction'] == 'NO_TRADE': return 0
        elif row['Prediction'] == row['Correct']: return abs(row['Result'])
        else: return -abs(row['Result'])
    result_df = df_subset[['Result', 'Correct']].copy()
    result_df['Prediction'] = final_predictions
    result_df['Profit'] = result_df.apply(calculate_profit, axis=1)
    return result_df

# -----------------------------
# メイン処理
# -----------------------------
def main():
    """メインの実行関数"""
    # --- 設定値 ---
    SYMBOL = '7203.JP'
    START_DATE = '2020-01-01'
    END_DATE = datetime.date.today().strftime('%Y-%m-%d')
    SPLIT_DATE = '2023-01-01'
    CONFIDENCE_THRESHOLD = 0.55

    # テクニカル指標のみを使用
    FEATURES = [
        'Close', 'High', 'Low', 'Open', 'Volume', 'SMA5', 'SMA20', 'MACD', 'RSI',
        'Upper', 'Lower', 'OBV', 'ATR', 'DayOfWeek', 
    ]
    
    MODELS = {
        "RandomForest": RandomForestClassifier(random_state=0),
        "LightGBM": lgb.LGBMClassifier(objective='multiclass', random_state=0, verbosity=-1),
    }

    PARAM_GRIDS = {
        "RandomForest": {'n_estimators': [100, 200], 'max_depth': [5, 10]},
        "LightGBM": {'n_estimators': [100, 200], 'max_depth': [5, 10], 'learning_rate': [0.1, 0.05]},
    }
    
    # 1. データ準備
    df_raw = get_stock_data(SYMBOL, START_DATE, END_DATE)
    
    global df
    df = add_technical_features(df_raw)

    # 欠損値(NaN)処理
    df.dropna(inplace=True)

    global le
    le = LabelEncoder()
    df['Correct_label'] = le.fit_transform(df['Correct'])

    train_df = df[df.index < SPLIT_DATE]
    valid_df = df[df.index >= SPLIT_DATE]
    
    if train_df.empty:
        print(f"エラー: 学習データが空です。START_DATE ({START_DATE}) をもっと過去にするか、SPLIT_DATE ({SPLIT_DATE}) を見直してください。")
        return

    X_train = train_df[FEATURES]
    y_train = train_df['Correct_label']
    X_valid = valid_df[FEATURES]
    y_valid = valid_df['Correct_label']

    # 2. モデルのチューニングと評価
    print("\n注意: モデルのチューニングには数分かかることがあります...")
    results_df, trained_models = tune_and_evaluate(X_train, y_train, X_valid, y_valid, MODELS, PARAM_GRIDS)
    print("\n--- モデルのチューニング結果 ---")
    print(results_df[['Model', 'Valid_Acc', 'Valid_Profit']])

    # 3. ベストモデルの選定と分析
    if results_df.empty or results_df['Valid_Profit'].isnull().all():
        print("有効なモデルが見つかりませんでした。")
        return

    best_model_name = results_df.sort_values("Valid_Profit", ascending=False).iloc[0]["Model"]
    best_model = trained_models[best_model_name]
    print(f"\n>>> 損益重視で選ばれたベストモデル: {best_model_name}")
    print("ベストパラメータ:", results_df.loc[results_df['Model'] == best_model_name, 'Best_Params'].values[0])
    
    analyze_feature_importance(best_model, FEATURES)

    # 4. リスク管理を適用した最終評価
    print("\n--- リスク管理を適用した最終評価 ---")
    risk_managed_results = apply_risk_management(best_model, X_valid, valid_df, CONFIDENCE_THRESHOLD)
    original_profit = results_df.loc[results_df['Model'] == best_model_name, 'Valid_Profit'].iloc[0]
    filtered_profit = risk_managed_results['Profit'].sum()
    trade_count = (risk_managed_results['Prediction'] != 'NO_TRADE').sum()
    win_rate = (risk_managed_results['Profit'] > 0).sum() / trade_count if trade_count > 0 else 0
    print(f"元の検証利益: {original_profit:.2f} 円")
    print(f"リスク管理後の検証利益: {filtered_profit:.2f} 円")
    print(f"取引回数: {len(X_valid)}回 -> {trade_count}回")
    print(f"勝率 (取引時のみ): {win_rate:.2%}")

    # 5. 明日の予測 (リスク管理適用)
    latest_features = df[FEATURES].iloc[-1:]
    tomorrow_prob = best_model.predict_proba(latest_features)[0]
    
    print("\n--- 明日の予測 ---")
    print(f"対象銘柄: {SYMBOL}")
    if tomorrow_prob.max() >= CONFIDENCE_THRESHOLD:
        tomorrow_pred_label = np.argmax(tomorrow_prob)
        tomorrow_pred_str = le.inverse_transform([tomorrow_pred_label])[0]
        print(f"予測: {tomorrow_pred_str} (信頼度: {tomorrow_prob.max():.2%})")
    else:
        print(f"予測: NO_TRADE (信頼度 {tomorrow_prob.max():.2%} がしきい値 {CONFIDENCE_THRESHOLD:.2%} 未満)")


if __name__ == "__main__":
    main()