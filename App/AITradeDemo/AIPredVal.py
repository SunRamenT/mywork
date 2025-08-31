import pandas as pd
import plotly.express as px
import requests
import json
import getpass
import datetime

def authenticate_jquants(email, password):
    """J-Quants APIの認証を行い、idTokenを取得する"""
    print("J-Quants APIの認証を実行します...")
    try:
        data = {"mailaddress": email, "password": password}
        req_post = requests.post("https://api.jquants.com/v1/token/auth_user", data=json.dumps(data))
        req_post.raise_for_status()
        refresh_token = req_post.json()["refreshToken"]
        
        req_post = requests.post(f"https://api.jquants.com/v1/token/auth_refresh?refreshtoken={refresh_token}")
        req_post.raise_for_status()
        id_token = req_post.json()["idToken"]
        print("認証に成功しました。")
        return id_token
    except Exception as e:
        print(f"エラー: J-Quants APIの認証に失敗しました。詳細: {e}")
        return None

def get_latest_fundamentals(id_token, code_list):
    """複数の銘柄の最新のファンダメンタル指標を取得する"""
    records = []
    headers = {'Authorization': 'Bearer {}'.format(id_token)}
    
    # J-Quantsは当日を含む過去2年分しか取得できないため、直近の平日を探す
    today = datetime.date.today()
    # 月曜日なら3日前(金曜)、日曜なら2日前(金曜)、土曜なら1日前(金曜)のデータを指定
    if today.weekday() == 0: # Monday
        latest_weekday = (today - datetime.timedelta(days=3)).strftime('%Y%m%d')
    elif today.weekday() == 6: # Sunday
        latest_weekday = (today - datetime.timedelta(days=2)).strftime('%Y%m%d')
    else: # Other weekdays
        latest_weekday = (today - datetime.timedelta(days=1)).strftime('%Y%m%d')

    print(f"直近のファンダメンタル指標（{latest_weekday}時点）を取得します...")

    for company_name, code in code_list.items():
        params = {'code': code, 'date': latest_weekday}
        
        try:
            response = requests.get("https://api.jquants.com/v1/prices/daily_quotes", params=params, headers=headers)
            response.raise_for_status()
            data = response.json().get('daily_quotes', [])
            
            if data:
                latest_data = data[0]
                records.append({
                    "会社名": company_name,
                    "銘柄コード": code,
                    "日付": latest_data.get('Date'),
                    "PER": latest_data.get('PER'),
                    "PBR": latest_data.get('PBR')
                })
            else:
                print(f"警告: {company_name}({code})のデータを取得できませんでした。")
        except requests.exceptions.RequestException as e:
            print(f"エラー: {company_name}({code})のデータ取得中にエラーが発生しました。 {e}")
            
    return pd.DataFrame(records)


# --- メイン処理 ---
if __name__ == "__main__":
    # --- J-Quants 認証情報 ---
    jquants_email = input("メールアドレスを入力してください: ")
    jquants_password = getpass.getpass("パスワードを入力してください (入力は表示されません): ")

    # 1. 認証
    id_token = authenticate_jquants(jquants_email, jquants_password)

    if id_token:
        # 2. 比較したい銘柄リストを定義
        automaker_codes = {
            "トヨタ": 7203,
            "ホンダ": 7267,
            "日産": 7201,
            "スズキ": 7269,
            "マツダ": 7261
        }

        # 3. データ取得とDataFrame作成
        df_fundamentals = get_latest_fundamentals(id_token, automaker_codes)
        
        # 数値型に変換し、欠損値は無視
        df_fundamentals['PBR'] = pd.to_numeric(df_fundamentals['PBR'], errors='coerce')
        df_fundamentals.dropna(subset=['PBR'], inplace=True)

        if not df_fundamentals.empty:
            # 業界平均を計算
            avg_pbr = df_fundamentals['PBR'].mean()
            print("\n--- 自動車業界 PBR比較 ---")
            print(df_fundamentals)
            print(f"\n業界平均PBR: {avg_pbr:.2f} 倍")

            # 4. グラフで可視化
            fig = px.bar(df_fundamentals.sort_values('PBR'),
                         x='会社名',
                         y='PBR',
                         title='自動車業界 PBR比較',
                         text='PBR')
            # 平均値の線を追加
            fig.add_hline(y=avg_pbr, line_dash="dot", 
                          annotation_text=f"業界平均: {avg_pbr:.2f}", 
                          annotation_position="bottom right")
            fig.show()
        else:
            print("比較対象のデータを取得できませんでした。")