import yfinance as yf
import pandas as pd
from tqdm import tqdm
import requests
from bs4 import BeautifulSoup
import os
import time
import warnings

# --- 初期設定 ---
# 不要な警告を非表示にする
warnings.filterwarnings('ignore')

# --- 1. TOPIX100銘柄コード取得 ---
print("--- TOPIX100の銘柄コードを取得しています ---")
try:
    url = "https://search.sbisec.co.jp/v2/popwin/info/stock/pop690_topix100.html"
    response = requests.get(url, timeout=10)
    response.raise_for_status()
    soup = BeautifulSoup(response.content, "html.parser")

    topix_100 = []
    for row in soup.select("table tr"):
        cols = row.find_all("td")
        if len(cols) > 1 and cols[0].text.strip().isdigit():
            code = cols[0].text.strip()
            topix_100.append(code)
    
    if not topix_100:
        raise ValueError("銘柄コードの取得に失敗しました。")
    print(f"TOPIX100銘柄数: {len(topix_100)}")

except requests.exceptions.RequestException as e:
    print(f"❌ ネットワークエラー: {e}")
    exit()
except Exception as e:
    print(f"❌ 銘柄コードの取得中にエラーが発生しました: {e}")
    exit()

# --- 2. 各銘柄の株価取得と特徴量作成 ---
print("\n--- 各銘柄の株価データを取得し、特徴量を作成しています ---")
data_list = []
for code in tqdm(topix_100, desc="銘柄データ取得中"):
    ticker = f"{code}.T"
    try:
        time.sleep(0.2) # APIへの負荷を軽減
        tmp = yf.download(ticker, period="1y", auto_adjust=True, progress=False)
        
        if tmp.empty or len(tmp) < 2:
            continue
            
        tmp.reset_index(inplace=True)
        tmp["code"] = code

        tmp["寄り引け変動率"] = (tmp["Close"] - tmp["Open"]) / tmp["Open"] * 100
        tmp["前日比"] = tmp["Close"].pct_change(1) * 100
        tmp.dropna(inplace=True)

        if tmp.empty:
            continue

        tmp = tmp[["Date", "code", "Open", "Close", "High", "Low", "寄り引け変動率", "前日比"]]
        data_list.append(tmp)
        
    except Exception:
        continue

if not data_list:
    raise ValueError("有効な株価データが1件も取得できませんでした。")

# --- 日本株データの準備 ---
df = pd.concat(data_list, ignore_index=True)
# タイムゾーン情報を削除し、日付でソートする（merge_asofの必須要件）
df["Date"] = pd.to_datetime(df["Date"]).dt.tz_localize(None)
df.sort_values("Date", inplace=True)


# --- 3. S&P500データの準備 ---
print("\n--- S&P500のデータを取得しています ---")
df_sp500 = yf.download("^GSPC", period="1y", auto_adjust=True, progress=False)
df_sp500.reset_index(inplace=True)
# こちらも同様にタイムゾーン情報を削除し、日付でソート
df_sp500["Date"] = pd.to_datetime(df_sp500["Date"]).dt.tz_localize(None)
df_sp500["S&P500前日比"] = df_sp500["Close"].pct_change() * 100
df_sp500.sort_values("Date", inplace=True)
df_sp500_for_merge = df_sp500[["Date", "S&P500前日比"]]


# --- 4. merge_asof を使った高度なマージ ---
print("\n--- 日米の営業日を考慮してデータをマージしています ---")
# ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
# 解決策: merge_asof を使用
# 各日本株の行(左のdf)に対して、その日付以前で最も近い
# S&P500のデータ(右のdf_sp500_for_merge)を自動で探して結合する。
# これにより、日米の祝日や週末の違いが吸収される。
# ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
df_merge = pd.merge_asof(
    df, 
    df_sp500_for_merge, 
    on="Date", 
    direction="backward" # backwardは「その日以前で直近の」を探すオプション
)
# マージによって発生したNaN（主にデータ期間の最初の方）を削除
df_merge.dropna(inplace=True)

if df_merge.empty:
    raise ValueError("マージ後、DataFrameが空になりました。データ期間や取得状況を確認してください。")

# --- 5. 年列とS&P上昇フラグの作成 ---
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