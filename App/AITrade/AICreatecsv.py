import datetime
import os
import pandas as pd
import numpy as np
import yfinance as yf
import matplotlib.pyplot as plt


# データ取得
target = "9279.T"
data = yf.download(target, period="30d", interval="1d")##ここのperiodのdをかえたら取得範囲もかわる

# マルチインデックスを解除
if isinstance(data.columns, pd.MultiIndex):
    data.columns = data.columns.get_level_values(0)

# 翌日の終値 - 翌日の始値
data["Result"] = data["Close"].shift(-1) - data["Open"].shift(-1)
data = data.iloc[:-1]  # 最終日を削除

# 学習データ
df_lea01 = data[["Close", "High", "Low", "Open"]].copy()
df_lea01.index.name = "Date"
df_lea01.index = df_lea01.index.strftime("%Y/%m/%d")

# 正解データ
def judge(x):
    if x > 0:
        return "UP"
    elif x < 0:
        return "DOWN"
    else:
        return "EVEN"

df_ans = pd.DataFrame({"Correct": data["Result"].apply(judge)})
df_ans.index.name = "Date"
df_ans.index = df_ans.index.strftime("%Y/%m/%d")

# スクリプトのフォルダパスを取得
folder = os.path.dirname(os.path.abspath(__file__))

# ファイルパスを作成
learning_path = os.path.join(folder, "Learning.csv")
answer_path = os.path.join(folder, "Answer.csv")

# CSV 出力
df_lea01.to_csv(learning_path, encoding="utf-8-sig")
df_ans.to_csv(answer_path, encoding="utf-8-sig")

