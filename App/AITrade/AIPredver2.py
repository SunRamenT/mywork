import yfinance as yf
import pandas as pd
from tqdm import tqdm
import requests
from bs4 import BeautifulSoup
import lightgbm as lgb
import datetime

# --- 1. SBI証券TOPIX100銘柄コード取得 ---
url = "https://search.sbisec.co.jp/v2/popwin/info/stock/pop690_topix100.html"
response = requests.get(url)
soup = BeautifulSoup(response.text, "html.parser")

topix_100 = []
for row in soup.find_all("tr")[1:]:
    cols = row.find_all("td")
    if len(cols) > 1:
        code = cols[0].text.strip()
        topix_100.append(code)

print(f"TOPIX100銘柄数: {len(topix_100)}")

# --- 2. 株価取得＆特徴量作成 ---
data_list = []
for code in tqdm(topix_100):
    ticker = f"{code}.T"
    try:
        tmp = yf.download(ticker, period="1y", auto_adjust=True, progress=False)
        if tmp.empty:
            continue
        tmp.reset_index(inplace=True)
        tmp["code"] = code
        # 寄り引け変動率
        tmp["寄り引け変動率"] = (tmp["Close"] - tmp["Open"]) / tmp["Open"] * 100
        # 前日比
        tmp["前日比"] = tmp["Close"].pct_change(1) * 100
        # 必要な列だけ残す
        tmp = tmp[["Date", "code", "Open", "Close", "寄り引け変動率", "前日比"]]
        data_list.append(tmp)
    except Exception as e:
        print(f"{code} 取得失敗: {e}")
        continue

df = pd.concat(data_list, ignore_index=True)
df["Date"] = pd.to_datetime(df["Date"])


# --- 3. S&P500データ取得 ---
df_sp500 = yf.download("^GSPC", period="1y", auto_adjust=True, progress=False)
df_sp500.reset_index(inplace=True)
df_sp500["Date"] = pd.to_datetime(df_sp500["Date"])
df_sp500["S&P500前日比"] = df_sp500["Close"].pct_change() * 100
# 1日シフト（前日比を翌日分に対応）
df_sp500_shifted = df_sp500[["Date", "S&P500前日比"]].shift(1)

# --- 4. マージ ---
df_merge = pd.merge(df, df_sp500_shifted, on="Date", how="left")

# --- 5. 年列とS&P上昇フラグ ---
df_merge["year"] = df_merge["Date"].dt.year
df_merge.loc[df_merge["S&P500前日比"] > 0, "S&P_up"] = 1
df_merge.loc[df_merge["S&P500前日比"] < 0, "S&P_up"] = 0

# --- 6. 保存（機械学習用に使える形） ---
df_merge.to_csv("topix100_features.csv", index=False, encoding="utf-8-sig")

print("✅ データ保存完了: topix100_features.csv")

target = "寄り引け変動率"
features = ['前日比','始値_終値','25日乖離率','S&P前日比'] #ほかにも特徴量は増やしたい

train_idx = df_merge.index < datetime.datetime(2025,1,1)
valid_idx = df_merge.set_index > datetime.datetime(2025,1,1)

df_train = df_merge.loc[train_idx,:]
df_valid = df_merge.loc[valid_idx,:]

params = [
    "learning_rate" : 0.01,
    "speed" : 1,
    "verbosity" : -1,
]

dtrain = lgb.Dataset(
    df_train[features],
    df_train[target],
)
dvalid = lgb.Dataset(
    df_valid[features],
    df_valid[target],
)

evals_result = []

model = lgb.train(
    params,
    dtrain,
    num_boost_roumd = 1000,
    valid_set=[dtrain,dvalid],
    valid_names=["train","valid"],
    early_stopping_rounds=100,
    evals_result=evals_result,
)

df_importance = pd.DataFrame()
df_importance["特徴量"] = model.feature_name()
df_importance["重要度"] = model.feature_importance(
    importance_type="gain"
)

print(df_importance.sort_values("重要度",ascending=False))