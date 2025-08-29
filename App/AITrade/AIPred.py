# ----------------------------- 
# ライブラリ
# -----------------------------
import pandas as pd
import datetime
import numpy as np
from pandas_datareader import data # type: ignore
from sklearn import tree
import talib as ta
import warnings
import os
warnings.simplefilter('ignore')

# ----------------------------- 
# 1. データ取得
# -----------------------------
start = '2022-01-01'
end = datetime.date.today().strftime('%Y-%m-%d')  # 今日の日付を文字列に変換
print("データ取得終了日:", end)  # 確認用
symbol = '9279.JP'

df = data.DataReader(symbol, 'stooq', start, end)
df = df.sort_index()  # 古い順に並び替え

# ----------------------------- 
# 2. テクニカル指標追加
# -----------------------------
df['SMA5'] = ta.SMA(df['Close'], timeperiod=5)
df['SMA20'] = ta.SMA(df['Close'], timeperiod=20)
df['High_diff'] = df['High'].diff()
df['Low_diff'] = df['Low'].diff()
df['Open_Close'] = df['Close'] - df['Open']
df['MA_diff'] = df['SMA5'] - df['SMA20']
df['SMA5_diff_ratio'] = (df['Close'] - df['SMA5']) / df['SMA5'] * 100
df['SMA20_diff_ratio'] = (df['Close'] - df['SMA20']) / df['SMA20'] * 100
df['MACD'], df['MACDSignal'], df['MACDHist'] = ta.MACD(df['Close'], fastperiod=12, slowperiod=26, signalperiod=9)
df['RSI'] = ta.RSI(df['Close'], timeperiod=20)
df['Upper'], df['Middle'], df['Lower'] = ta.BBANDS(df['Close'], timeperiod=20, nbdevup=2, nbdevdn=2, matype=0)

# ----------------------------- 
# 3. 目的変数作成
# -----------------------------
df['Result'] = df['Close'].shift(-1) - df['Open'].shift(-1)
df['Correct'] = df['Result'].apply(lambda x: 'UP' if x>0 else ('DOWN' if x<0 else 'EVEN'))

# ----------------------------- 
# 4. 欠損値削除
# -----------------------------
df = df.dropna()

# ----------------------------- 
# 5. 学習 / 検証データ分割
# -----------------------------
split_date = '2025-08-01' 
train_df = df[df.index < split_date]
valid_df = df[df.index >= split_date]

features = ['Close','High','Low','Open','SMA5','SMA20','High_diff','Low_diff',
            'Open_Close','MA_diff','SMA5_diff_ratio','SMA20_diff_ratio',
            'MACD','MACDSignal','MACDHist','RSI','Upper','Middle','Lower']

X_train = train_df[features]
y_train = train_df['Correct']
X_valid = valid_df[features]
y_valid = valid_df['Correct']

# ----------------------------- 
# 6. モデル学習
# -----------------------------
model = tree.DecisionTreeClassifier(max_depth=10, random_state=0)
model.fit(X_train, y_train)

# ----------------------------- 
# 7. 予測
# -----------------------------
train_df['Predict'] = model.predict(X_train)
valid_df['Predict'] = model.predict(X_valid)

# ----------------------------- 
# 8. 損益計算関数
# -----------------------------
def calc_profit(df):
    df['Profit'] = np.where(df['Predict'] == df['Correct'], df['Close'] - df['Open'], -(df['Close'] - df['Open']))
    return df['Profit'].sum()

train_profit = calc_profit(train_df)
valid_profit = calc_profit(valid_df)

# ----------------------------- 
# 9. 明日の予測（UP/DOWNのみ）
# -----------------------------
latest_features = df[features].iloc[-1:]  # 最終日の特徴量
tomorrow_pred = model.predict(latest_features)[0]

# ----------------------------- 
# 10. CSV保存
# -----------------------------
summary_df = pd.concat([
    train_df[['Close','Open','Correct','Predict','Profit']],
    valid_df[['Close','Open','Correct','Predict','Profit']]
])
summary_df.to_csv(os.path.join(os.path.dirname(os.path.abspath(__file__)), "Profit_Summary.csv"),
                  encoding="utf-8-sig", index_label="Date")

# ----------------------------- 
# 11. 結果表示
# -----------------------------
print("学習データ精度:", model.score(X_train, y_train))
print("検証データ精度:", model.score(X_valid, y_valid))
print("学習データ損益:", train_profit, "円")
print("検証データ損益:", valid_profit, "円")
print(valid_df[['Close','Open','Correct','Predict','Profit']].tail(5))

print("\n--- 明日の予測 ---")
print("予測:", tomorrow_pred)
