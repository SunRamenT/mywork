import yfinance as yf
import pandas as pd
import requests
from bs4 import BeautifulSoup
import os
import warnings

# --- 初期設定 ---
warnings.filterwarnings('ignore')

# データ取得期間を固定
START_DATE = "2023-08-01"
END_DATE = "2024-08-01"

# --- 1. TOPIX100銘柄コード取得 ---
print("--- TOPIX100の銘柄コードを取得しています ---")
try:
    url = "https://search.sbisec.co.jp/v2/popwin/info/stock/pop690_topix100.html"
    response = requests.get(url, timeout=15)
    response.raise_for_status()
    soup = BeautifulSoup(response.content, "html.parser")

    topix_100 = [
        cols[0].text.strip()
        for row in soup.select("table tr")
        if (cols := row.find_all("td")) and len(cols) > 1 and cols[0].text.strip().isdigit()
    ]
    
    if not topix_100:
        raise ValueError("銘柄コードの取得に失敗しました。")
    print(f"TOPIX100銘柄数: {len(topix_100)}")

except Exception as e:
    print(f"❌ 銘柄コードの取得中にエラーが発生しました: {e}")
    exit()

# --- 2. 全銘柄の株価データを一括取得 ---
print(f"\n--- 全銘柄の株価データを一括で取得しています（期間: {START_DATE} ～ {END_DATE}）---")
tickers_with_suffix = [f"{code}.T" for code in topix_100]
raw_data = yf.download(tickers_with_suffix, start=START_DATE, end=END_DATE, auto_adjust=True)

print("\n--- 取得したデータを分析しやすい形式に変換しています ---")
raw_data.columns.names = ['feature', 'code']
df = raw_data.stack(level='code').reset_index()
df['code'] = df['code'].str.replace('.T', '', regex=False)

# --- 3. 特徴量の計算 ---
print("\n--- 特徴量を計算しています ---")
df = df.sort_values(['code', 'Date'])
df['前日比'] = df.groupby('code')['Close'].pct_change(1) * 100
df['寄り引け変動率'] = (df['Close'] - df['Open']) / df['Open'] * 100
df.dropna(subset=['前日比', '寄り引け変動率'], inplace=True)
df = df[["Date", "code", "Open", "Close", "High", "Low", "寄り引け変動率", "前日比"]]
df["Date"] = pd.to_datetime(df["Date"]).dt.tz_localize(None)


# --- 4. S&P500データの準備とマージ ---
print("\n--- S&P500のデータを取得し、マージしています ---")
raw_sp500 = yf.download("^GSPC", start=START_DATE, end=END_DATE, auto_adjust=True, progress=False)

# ★★★★★★★★★★★★★★★★★ 最終解決策 ★★★★★★★★★★★★★★★★★
# yfinanceが返す可能性のある複雑な列構造（MultiIndex）を強制的に単純化する
# 必要な'Close'列だけを持つ新しいDataFrameを作成し、列構造を保証する
df_sp500 = pd.DataFrame(index=raw_sp500.index)
df_sp500['Close'] = raw_sp500['Close']
df_sp500.reset_index(inplace=True)
# ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

df_sp500["Date"] = pd.to_datetime(df_sp500["Date"]).dt.tz_localize(None)
df_sp500["S&P500前日比"] = df_sp500["Close"].pct_change() * 100
df_sp500.sort_values("Date", inplace=True)
df_sp500_for_merge = df_sp500[["Date", "S&P500前日比"]]

# merge_asof を使い、各日本株の営業日に直近のS&P500データを結合
df_merge = pd.merge_asof(
    df.sort_values('Date'), 
    df_sp500_for_merge.dropna(), 
    on="Date", 
    direction="backward"
)
df_merge.dropna(inplace=True)

if df_merge.empty:
    raise ValueError("マージ後、DataFrameが空になりました。")

# --- 5. 最終的な列の追加 ---
df_merge["year"] = df_merge["Date"].dt.year
df_merge["S&P_up"] = (df_merge["S&P500前日比"] > 0).astype(int)

# --- 6. CSVファイルへの保存 ---
try:
    script_dir = os.path.dirname(os.path.abspath(__file__))
except NameError:
    script_dir = os.getcwd()

output_path = os.path.join(script_dir, "topix100_features_basic.csv")
df_merge.to_csv(output_path, index=False, encoding="utf-8-sig")

print(f"\n✅ データ保存完了: {output_path}")
print("\n--- 生成されたデータフレームの先頭5行 ---")
print(df_merge.head())
print("\n--- データフレームの基本情報 ---")
df_merge.info()
