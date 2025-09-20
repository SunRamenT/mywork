#include <Siv3D.hpp>

// ヒットエフェクトの設計図
// IEffectを継承することで、Siv3Dのエフェクトシステム(Effect)で使えるようになる
struct EmojiEmitter : IEffect
{
	// メンバー変数: エフェクトが必要とする情報を保持する
	Vec2 m_pos;      // エフェクトの発生座標
	String m_emoji;  // 表示する絵文字
	const Font& m_font; // 描画に使うフォントへの参照

	// コンストラクタ: エフェクトが生成される瞬間に一度だけ呼ばれる
	// ここで必要な情報を受け取る
	EmojiEmitter(const Vec2& pos, const String& emoji, const Font& font)
		: m_pos{ pos }
		, m_emoji{ emoji }
		, m_font{ font } {
	}

	// 更新処理: エフェクトが存在する間、毎フレーム呼ばれる
	// 戻り値が false になるとエフェクトが消滅する
	bool update(double t) override
	{
		// t: エフェクトが発生してからの経過時間（秒）

		// EaseOutQuad: だんだん遅くなる動きを表現するための計算
		const double e = EaseOutQuad(t);
		// 少しずつ上に移動しながらフェードアウトする演出
		m_font(m_emoji).drawAt(m_pos.movedBy(0, -e * 40), ColorF{ 1.0, 1.0 - e });

		// tが0.5秒未満なら true (エフェクトを継続), 0.5秒以上なら false (エフェクトを消滅)
		// この値を変えるとエフェクトの表示時間が変わる
		return (t < 0.5);
	}
};

// 敵1体の情報をまとめる設計図 (構造体)
struct Enemy
{
	Vec2 pos;           // 敵の現在位置
	Circle body;        // 敵の当たり判定
	double hp;          // 現在のHP
	double maxHp;       // 最大HP
	Timer attackTimer;  // 次の攻撃までのタイマー
	RectF attackZone;   // 攻撃の予告範囲
	bool isGuarding = false; // プレイヤーがこの攻撃をガードできているか

	// 敵の現在の状態 (3パターン)
	enum class State { Idle, Warning, Attacking };
	State state = State::Idle; // 初期状態はIdle

	Texture texture;    // 敵の画像
};

// ゲーム全体のシーン（場面）を定義
enum class GameState
{
	Tutorial, // チュートリアル画面
	Stage1,   // ステージ1
	Stage2,   // ステージ2
	Clear,    // ゲームクリア画面
};

void Main()
{
	// --- 初期設定 & アセット準備 ---
	// このブロックにあるものは、ゲーム起動時に一度だけ読み込まれる

	// ウィンドウの上部に表示されるタイトル
	Window::SetTitle(U"戦闘システム - 完成版");
	// ゲームの背景色
	Scene::SetBackground(ColorF{ 0.1, 0.1, 0.1 });

	// ゲーム内で使用するフォントを3種類、あらかじめ作成しておく
	const Font font{ 30, Typeface::Bold };       // 通常のUI用
	const Font bigFont{ 50, Typeface::Bold };    // ゲームオーバー/クリア表示用
	const Font effectFont{ 40, Typeface::Bold }; // エフェクト用

	// 敵の画像を絵文字から作成
	const Texture enemyTexture{ U"👾"_emoji };

	// --- ゲーム全体で共有する変数 ---
	// ゲーム中に変化したり、シーンをまたいで使われたりする情報

	// プレイヤーのHP。この値を変えると初期HPが変わる
	double playerHP = 200;
	const double playerMaxHP = 200;

	// プレイヤーの入力状態を管理する変数
	Vec2 mouseDownPos{ 0, 0 }; // マウスをクリックした最初の場所
	Stopwatch pressStopwatch;  // マウスを押し続けている時間を計る

	// 長押し攻撃と判定されるまでの秒数。1.5にすれば、より長く押す必要が出てくる
	const double longPressThreshold = 1.0;
	bool isLongPressing = false; // 現在、長押し攻撃中かどうかのフラグ
	Circle longPressAoE;         // 長押し攻撃の当たり判定とエフェクト

	// 全てのエフェクトを管理するオブジェクト
	Effect effect;

	// 現在のシーンを管理する変数。この値を変えることでシーンが切り替わる
	GameState currentState = GameState::Tutorial;

	// ステージに登場する全ての敵をまとめて管理するリスト
	Array<Enemy> enemies;

	// この while ブロックの中がゲームの本体。1秒間に60回、猛スピードで繰り返し実行される
	while (System::Update())
	{
		// 見た目以外の、ゲームの内部的な状態変化や計算をここで行う
		// 現在のシーン(currentState)に応じて、実行する処理を切り替える
		switch (currentState)
		{
			//チュートリアルシーンの処理
		case GameState::Tutorial:
			// もしマウスの左ボタンがクリックされたら
			if (MouseL.down())
			{
				// 敵リストを一旦空にする
				enemies.clear();
				// 敵リストにステージ1の敵を1体追加する
				enemies.push_back(Enemy{
					.pos = Scene::Center(),                     // 登場位置
					.body = Circle{ Scene::Center(), 50 },      // 当たり判定の大きさ
					.hp = 500, .maxHp = 500,                    // HP
					.attackTimer = Timer{ 3s, StartImmediately::Yes }, // 最初の攻撃までの時間
					.texture = enemyTexture                     // 画像
					});
				// シーンをステージ1に切り替える
				currentState = GameState::Stage1;
			}
			break;

			//ゲーム中（ステージ1と2共通）の処理
		case GameState::Stage1:
		case GameState::Stage2:

			// --- プレイヤーの入力処理 ---
			if (MouseL.down()) {
				mouseDownPos = Cursor::Pos();
				pressStopwatch.start();
				isLongPressing = false;
			}

			if (MouseL.pressed()) {
				// 押している時間がしきい値を超えたら、長押し攻撃モードへ
				if (pressStopwatch.sF() > longPressThreshold) {
					isLongPressing = true;
				}

				// 長押し攻撃モード中の処理
				if (isLongPressing) {
					// 長押し開始からの経過時間を計算
					const double chargeTime = pressStopwatch.sF() - longPressThreshold;
					// 時間経過で攻撃範囲が大きくなる (初期半径20, 1秒ごとに半径が50拡大)
					const double radius = 20.0 + chargeTime * 50.0;
					// 攻撃範囲の位置を現在カーソルに合わせる
					longPressAoE.set(Cursor::Pos(), radius);

					// 全ての敵に対して当たり判定をチェック
					for (auto& enemy : enemies) {
						if (enemy.body.intersects(longPressAoE)) {
							// 1秒あたり80ダメージ。この値を変えると攻撃力が変わる
							const double damagePerSecond = 80.0;
							// 毎フレーム少しずつHPを削る (フレームレートに依存しないようにScene::DeltaTime()を掛ける)
							enemy.hp -= damagePerSecond * Scene::DeltaTime();
							// 10フレームに1回エフェクトを出す
							if (Scene::FrameCount() % 10 == 0) {
								effect.add<EmojiEmitter>(enemy.body.center, U"🔥", effectFont);
							}
						}
					}
				}
			}

			if (MouseL.up()) {
				// 長押し攻撃をしていなかった場合のみ、スライドやちょい押しを判定
				if (!isLongPressing) {
					const Vec2 dragVector = Cursor::Pos() - mouseDownPos;
					const double dragDistance = dragVector.length();

					// スライド距離が60ピクセル以上ならスライド攻撃
					if (dragDistance > 60) {
						// スライドの軌跡を「線」として作成
						const Line trajectory{ mouseDownPos, Cursor::Pos() };
						// 全ての敵に対して当たり判定をチェック
						for (auto& enemy : enemies) {
							// 敵の中心と軌跡の距離が「敵の半径 + 剣の太さ」より近ければヒット
							// 20.0が剣の太さ。この値を大きくすると当たりやすくなる
							if (Geometry2D::Distance(enemy.body.center, trajectory) < (enemy.body.r + 20.0)) {
								// 横スライドか縦スライドかでダメージ量を変える
								double damage = (Abs(dragVector.x) > Abs(dragVector.y)) ? 70 : 50;
								enemy.hp -= damage;
								effect.add<EmojiEmitter>(enemy.body.center, U"💥", effectFont);
							}
						}
					}
					else { // スライド距離が短ければちょい押し攻撃
						// 全ての敵に対して当たり判定をチェック
						for (auto& enemy : enemies) {
							// カーソル位置の半径40の円が敵に当たっていればヒット
							if (enemy.body.intersects(Circle{ Cursor::Pos(), 40 })) {
								enemy.hp -= 30; // ちょい押し攻撃のダメージ量
								effect.add<EmojiEmitter>(enemy.body.center, U"💥", effectFont);
							}
						}
					}
				}
				// どの攻撃であっても、ボタンを離したら状態をリセット
				pressStopwatch.reset();
				isLongPressing = false;
			}

			// --- 敵のAI処理 ---
			// forループで、リストにいる敵一体一体を個別に処理する
			for (auto& enemy : enemies)
			{
				// 敵が待機状態(Idle)なら
				if (enemy.state == Enemy::State::Idle) {
					// 攻撃タイマーが0になったら
					if (enemy.attackTimer.reachedZero()) {
						// 攻撃の予告状態(Warning)に移行
						enemy.state = Enemy::State::Warning;
						// 0(上),1(下),2(左),3(右)のランダムな方向を決定
						const int32 dir = Random(3);
						// 敵の位置から180離れた場所に、150x150の攻撃ゾーンを作る
						enemy.attackZone = RectF{ Arg::center = enemy.pos.movedBy(Vec2{0, 180}.rotated(90_deg * dir)), Size{150, 150} };
						// ガード受付時間を0.5秒にセットしてタイマースタート
						enemy.attackTimer.set(0.5s);
						enemy.attackTimer.start();
						enemy.isGuarding = false;
					}
				}
				// 敵が予告状態(Warning)なら
				else if (enemy.state == Enemy::State::Warning) {
					// 予告範囲にマウスカーソルが乗っていればガード成功
					if (enemy.attackZone.mouseOver()) {
						enemy.isGuarding = true;
					}
					// ガード受付時間が終わったら
					if (enemy.attackTimer.reachedZero()) {
						// 攻撃発生状態(Attacking)に移行
						enemy.state = Enemy::State::Attacking;
					}
				}
				// 敵が攻撃発生状態(Attacking)なら
				else if (enemy.state == Enemy::State::Attacking) {
					// もしガード失敗していたらプレイヤーにダメージ
					if (!enemy.isGuarding) {
						playerHP -= 40; // 敵の攻撃力
					}
					// 待機状態(Idle)に戻る
					enemy.state = Enemy::State::Idle;
					// 次の攻撃までの時間を2秒から4秒のランダムに設定
					enemy.attackTimer.set(Random(2.0, 4.0) * 1s);
					enemy.attackTimer.start();
				}
			}

			// HPが0以下の敵をリストから削除する
			enemies.erase(std::remove_if(enemies.begin(), enemies.end(), [](const Enemy& e) { return e.hp <= 0; }), enemies.end());

			// ステージクリア判定: 敵リストが空になったら
			if (enemies.isEmpty())
			{
				// 現在がステージ1だったら
				if (currentState == GameState::Stage1)
				{
					// 敵リストを空にして、ステージ2の敵を2体追加する
					enemies.clear();
					enemies.push_back(Enemy{ // 左の敵
						.pos = Scene::Center().movedBy(-200, 0),
						.body = Circle{ Scene::Center().movedBy(-200, 0), 50 },
						.hp = 300, .maxHp = 300,
						.attackTimer = Timer{ 2s, StartImmediately::Yes },
						.texture = enemyTexture
						});
					enemies.push_back(Enemy{ // 右の敵
						.pos = Scene::Center().movedBy(200, 0),
						.body = Circle{ Scene::Center().movedBy(200, 0), 50 },
						.hp = 300, .maxHp = 300,
						.attackTimer = Timer{ 3.5s, StartImmediately::Yes },
						.texture = enemyTexture
						});
					// シーンをステージ2に切り替える
					currentState = GameState::Stage2;
				}
				// 現在がステージ2だったら
				else if (currentState == GameState::Stage2)
				{
					// クリアシーンに切り替える
					currentState = GameState::Clear;
				}
			}
			break;

			// クリアシーンの処理
		case GameState::Clear:
			// マウスがクリックされたらゲームを終了する
			if (MouseL.down())
			{
				System::Exit();
			}
			break;
		}

		// エフェクトの更新（これを毎フレーム呼ばないとエフェクトが動かない）
		effect.update();

		// --- 描画部分 ---
		// ゲームの見た目に関する処理をここで行う

		// 現在のシーンに応じて、描画する内容を切り替える
		switch (currentState)
		{
			//チュートリアルシーンの描画
		case GameState::Tutorial:
			bigFont(U"操作方法").drawAt(Scene::Center().x, 100);
			font(U"マウスをクリック: 拳攻撃").drawAt(Scene::Center().x, 200);
			font(U"マウスをスライド: 縦:打撃/横:剣撃").drawAt(Scene::Center().x, 250);
			font(U"マウス長押し (1秒以上): 魔法攻撃").drawAt(Scene::Center().x, 300);
			font(U"[!]ゾーンにカーソルを合わせる: 防御").drawAt(Scene::Center().x, 350);
			font(U"\nクリックでスタート").drawAt(Scene::Center().x, 450);
			break;

			//ゲーム中（ステージ1と2共通）の描画
		case GameState::Stage1:
		case GameState::Stage2:
			// プレイヤーHPバー
			// 背景の赤いバー
			RectF{ 10, 10, 300, 30 }.draw(Palette::Red);
			// 前面の緑のバー（HPの割合で幅が変わる）
			RectF{ 10, 10, (playerHP / playerMaxHP) * 300, 30 }.draw(Palette::Green);
			// ラベル
			font(U"Player HP").drawAt(160, 25);
			// HP数値
			{
				const String hpText = U"{} / {}"_fmt(static_cast<int32>(playerHP), static_cast<int32>(playerMaxHP));
				font(hpText).draw(10 + 300 + 10, 10, Palette::White);
			}

			// forループで、リストにいる敵一体一体を描画する
			for (const auto& enemy : enemies)
			{
				// 敵の頭上にHPバーを描画
				const RectF enemyHpBarArea{ Arg::center = enemy.pos.movedBy(0, -60), 120, 15 };
				enemyHpBarArea.draw(Palette::Red);
				enemyHpBarArea.stretched(-2).draw(ColorF{ 0.2 });
				// HPが0未満にならないようにMax(0.0, ...)で計算
				const double displayHp = Max(0.0, enemy.hp);
				RectF{ enemyHpBarArea.pos, enemyHpBarArea.w * (displayHp / enemy.maxHp), enemyHpBarArea.h }.draw(Palette::Limegreen);

				// 敵本体の画像を描画
				enemy.texture.resized(100).drawAt(enemy.pos);
				// (デバッグ用) 敵の当たり判定を半透明の赤色で可視化
				enemy.body.draw(ColorF{ 1, 0, 0, 0.3 });

				// 敵が予告状態なら、攻撃ゾーンを描画
				if (enemy.state == Enemy::State::Warning) {
					enemy.attackZone.draw(ColorF{ 1, 0.2, 0, 0.5 });
					font(U"!").drawAt(enemy.attackZone.center());
					// ガード成功エフェクト
					if (enemy.isGuarding) {
						Circle{ Cursor::Pos(), 40 }.drawFrame(5, Palette::Cyan);
					}
				}
			}

			// プレイヤーの操作エフェクト
			// 長押し攻撃中でなければ、スライドの軌跡を描画
			if (MouseL.pressed() && !isLongPressing) {
				Line{ mouseDownPos, Cursor::Pos() }.draw(4, Palette::Orange);
			}
			// 長押し攻撃中なら、攻撃範囲を描画
			if (isLongPressing) {
				longPressAoE.draw(ColorF{ 1.0, 0.5, 0.0, 0.5 });
			}

			// プレイヤーのHPが0になったらゲームオーバー表示
			if (playerHP <= 0) {
				Rect{ Scene::Size() }.draw(ColorF{ 0.0, 0.5 });
				bigFont(U"GAME OVER").drawAt(Scene::Center(), Palette::White);
				System::Exit(); // ゲームを強制終了
			}
			break;

			//クリアシーンの描画
		case GameState::Clear:
			bigFont(U"GAME CLEAR!").drawAt(Scene::Center(), Palette::White);
			font(U"クリックで終了").drawAt(Scene::Center().x, 400);
			break;
		}
	}
}
