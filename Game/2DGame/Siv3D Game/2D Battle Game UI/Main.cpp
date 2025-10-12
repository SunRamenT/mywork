#include <Siv3D.hpp>

// ----------------------------------------------------------------
// ■ ヒットエフェクトの設計図 (IEffect を継承)
// ----------------------------------------------------------------
// Siv3D の Effect 機能で使えるようにするためのカスタムクラス。
// `effect.add<EmojiEmitter>(...)` のように呼び出すと、
// このクラスのインスタンスが生成され、自動的に update が呼ばれる。
struct EmojiEmitter : IEffect
{
	// --- メンバー変数 ---
	Vec2 m_pos;      // エフェクトの発生座標
	String m_emoji;  // 表示する絵文字
	const Font& m_font; // 描画に使うフォントへの参照

	// --- コンストラクタ ---
	// エフェクトが生成される瞬間に一度だけ呼ばれる
	EmojiEmitter(const Vec2& pos, const String& emoji, const Font& font)
		: m_pos{ pos }
		, m_emoji{ emoji }
		, m_font{ font } {
	}

	// --- 更新処理 ---
	// エフェクトが存在する間、毎フレーム呼ばれる
	// 戻り値が false になるとエフェクトが消滅する
	bool update(double t) override
	{
		// t: エフェクトが発生してからの経過時間（秒）

		// EaseOutQuad: だんだん遅くなる動きを表現するための計算
		const double e = EaseOutQuad(t);
		// 少しずつ上に移動しながら、徐々に透明になって消える演出
		m_font(m_emoji).drawAt(m_pos.movedBy(0, -e * 40), ColorF{ 1.0, 1.0 - e });

		// t が 0.5 秒未満なら true (エフェクト継続), 0.5秒以上なら false (エフェクト消滅)
		return (t < 0.5);
	}
};

// ----------------------------------------------------------------
// ■ 敵1体の情報をまとめる設計図 (構造体)
// ----------------------------------------------------------------
struct Enemy
{
	// --- 基本情報 ---
	Vec2 pos;         // 敵の現在位置 (毎フレーム変わる可能性がある)
	Vec2 initialPos;  // 敵の初期位置 (ステージ3の動きの基準点として使う)
	Circle body;      // 敵の当たり判定
	double hp;        // 現在のHP
	double maxHp;     // 最大HP
	Texture texture;  // 敵の画像

	// --- AI関連 ---
	Timer attackTimer;  // 次の行動までの時間を管理するタイマー
	RectF attackZone;   // 攻撃の予告範囲
	int32 attackDir = 0;// 攻撃方向 (0:上, 1:下, 2:左, 3:右) を保持
	bool isGuarding = false; // プレイヤーがこの攻撃をガードできているか

	// 敵の現在の状態を3パターンで管理
	enum class State { Idle, Warning, Attacking };
	State state = State::Idle; // 初期状態は Idle (待機)
};

// ----------------------------------------------------------------
// ■ ゲーム全体のシーン（場面）を定義
// ----------------------------------------------------------------
enum class GameState
{
	Tutorial, // チュートリアル画面
	Stage1,   // ステージ1
	Stage2,   // ステージ2
	Stage3,   // ステージ3
	Clear,    // ゲームクリア画面
	GameOver, // ゲームオーバー画面
};

// ----------------------------------------------------------------
// ■ メインループ
// ----------------------------------------------------------------
void Main()
{
	// --- 初期設定 & アセット準備 ---
	// このブロックにあるものは、ゲーム起動時に一度だけ実行される
	Window::SetTitle(U"戦闘システム - 改良版");
	Scene::SetBackground(ColorF{ 0.1, 0.1, 0.1 });

	// ゲーム内で使用するフォントを3種類、あらかじめ作成しておく
	const Font font{ 30, Typeface::Bold };
	const Font bigFont{ 50, Typeface::Bold };
	const Font effectFont{ 40, Typeface::Bold };

	// 敵の画像を絵文字から作成
	const Texture enemyTexture{ U"👾"_emoji };


	// --- ゲーム全体で共有する変数 ---
	// ゲーム中に変化したり、シーンをまたいで使われたりする情報
	double playerHP = 200;
	const double playerMaxHP = 200;

	// プレイヤーの入力状態を管理する変数群
	Vec2 mouseDownPos{ 0, 0 }; // マウスをクリックした最初の場所
	Stopwatch pressStopwatch;  // マウスを押し続けている時間を計る
	const double longPressThreshold = 1.0; // 長押し攻撃と判定されるまでの秒数
	bool isLongPressing = false; // 現在、長押し攻撃中かどうかのフラグ
	Circle longPressAoE;         // 長押し攻撃の当たり判定とエフェクト

	// 全てのエフェクトを管理するオブジェクト
	Effect effect;

	// 現在のシーンを管理する変数。この値を変えることでシーンが切り替わる
	GameState currentState = GameState::Tutorial;

	// ステージに登場する全ての敵をまとめて管理するリスト (可変長配列)
	Array<Enemy> enemies;


	// この while ブロックの中がゲームの本体。1秒間に約60回、猛スピードで繰り返し実行される
	while (System::Update())
	{
		// ================================================================
		//  §1. ロジック更新 (計算、状態変化など)
		// ================================================================
		// 現在のシーン(currentState)に応じて、実行する処理を切り替える
		switch (currentState)
		{
			// --- チュートリアルシーンの処理 ---
		case GameState::Tutorial:
			// もしマウスの左ボタンがクリックされたら
			if (MouseL.down())
			{
				// 敵リストを一旦空にする
				enemies.clear();
				// ステージ1の敵を1体作成してリストに追加する
				enemies.push_back(Enemy{
					.pos = Scene::Center(),
					.initialPos = Scene::Center(),
					.body = Circle{ Scene::Center(), 50 },
					.hp = 500,
					.maxHp = 500,
					.texture = enemyTexture,
					.attackTimer = Timer{ 3s, StartImmediately::Yes },
					});
				// シーンをステージ1に切り替える
				currentState = GameState::Stage1;
			}
			break;

			// --- ゲーム中（ステージ1, 2, 3共通）の処理 ---
		case GameState::Stage1:
		case GameState::Stage2:
		case GameState::Stage3:

			// ▼▼▼ ステージ3のみの特殊な処理 ▼▼▼
			if (currentState == GameState::Stage3)
			{
				// すべての敵をループで動かす
				for (auto& enemy : enemies)
				{
					// Sin波を使って滑らかな左右往復運動をさせる
					// Scene::Time() : ゲーム開始からの経過時間(秒)
					// Sin() の結果は -1.0 ~ 1.0 の範囲で変化するので、敵が左右に揺れる
					enemy.pos.x = enemy.initialPos.x + Sin(Scene::Time() * 2.0) * 150;
					enemy.body.setPos(enemy.pos); // 当たり判定も本体の座標に追従させる
				}
			}
			// ▲▲▲ ステージ3のみの特殊な処理 ▲▲▲


			// --- プレイヤーの入力処理 ---
			if (MouseL.down()) { // マウスが押された瞬間
				mouseDownPos = Cursor::Pos();
				pressStopwatch.start();
				isLongPressing = false; // 長押し状態をリセット
			}

			if (MouseL.pressed()) { // マウスが押されている間
				// 押している時間がしきい値(1秒)を超えたら、長押し攻撃モードへ
				if (not isLongPressing && pressStopwatch.sF() > longPressThreshold) {
					isLongPressing = true;
				}

				// 長押し攻撃モード中の処理
				if (isLongPressing) {
					const double chargeTime = pressStopwatch.sF() - longPressThreshold;
					const double radius = 20.0 + chargeTime * 50.0; // 時間経過で攻撃範囲が拡大
					longPressAoE.set(Cursor::Pos(), radius);

					// 全ての敵に対して当たり判定をチェック
					for (auto& enemy : enemies) {
						if (enemy.body.intersects(longPressAoE)) {
							const double damagePerSecond = 80.0;
							// 毎フレーム少しずつHPを削る (フレームレートに依存しないようにScene::DeltaTime()を掛ける)
							enemy.hp -= damagePerSecond * Scene::DeltaTime();
							// 負荷軽減のため、10フレームに1回だけエフェクトを出す
							if (Scene::FrameCount() % 10 == 0) {
								effect.add<EmojiEmitter>(enemy.body.center, U"🔥", effectFont);
							}
						}
					}
				}
			}

			if (MouseL.up()) { // マウスが離された瞬間
				// 長押し攻撃をしていなかった場合のみ、スライドやちょい押しを判定
				if (not isLongPressing) {
					const Vec2 dragVector = Cursor::Pos() - mouseDownPos;
					const double dragDistance = dragVector.length();

					if (dragDistance > 60) { // スライド距離が60ピクセル以上ならスライド攻撃
						const Line trajectory{ mouseDownPos, Cursor::Pos() };
						for (auto& enemy : enemies) {
							// 敵の中心とスライド軌跡の距離で当たり判定
							if (Geometry2D::Distance(enemy.body.center, trajectory) < (enemy.body.r + 20.0)) {
								// 横スライドか縦スライドかでダメージ量を変える
								double damage = (Abs(dragVector.x) > Abs(dragVector.y)) ? 70 : 50;
								enemy.hp -= damage;
								effect.add<EmojiEmitter>(enemy.body.center, U"💥", effectFont);
							}
						}
					}
					else { // スライド距離が短ければちょい押し攻撃
						for (auto& enemy : enemies) {
							if (enemy.body.intersects(Circle{ Cursor::Pos(), 40 })) {
								enemy.hp -= 30;
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
			for (auto& enemy : enemies)
			{
				// 【状態1: Idle (待機)】
				if (enemy.state == Enemy::State::Idle) {
					// 攻撃タイマーが0になったら
					if (enemy.attackTimer.reachedZero()) {
						// 攻撃の予告状態(Warning)に移行
						enemy.state = Enemy::State::Warning;
						enemy.attackDir = Random(3); // 0(上),1(下),2(左),3(右)のランダムな方向を決定
						// 敵の位置から180離れた場所に、150x150の攻撃ゾーンを作る
						enemy.attackZone = RectF{ Arg::center = enemy.pos.movedBy(Vec2{0, 180}.rotated(90_deg * enemy.attackDir)), Size{150, 150} };
						enemy.attackTimer.set(0.5s); // ガード受付時間を0.5秒にセット
						enemy.attackTimer.start();
						enemy.isGuarding = false; // ガード状態をリセット
					}
				}
				// 【状態2: Warning (攻撃予告)】
				else if (enemy.state == Enemy::State::Warning) {

					// ▼▼▼ ステージ3のみの攻撃予告の動き ▼▼▼
					if (currentState == GameState::Stage3)
					{
						// 予告タイマーの進捗(0.0 ~ 1.0)を取得
						const double progress = enemy.attackTimer.progress0_1();
						// 攻撃ゾーンの基準となる中心座標を計算
						const Vec2 centerBase = enemy.pos.movedBy(Vec2{ 0, 180 }.rotated(90_deg * enemy.attackDir));
						// (progress - 0.5) は -0.5 ~ 0.5 の範囲で変化する。
						// これにより、攻撃予告が表示されている間にゾーンが左右にスライドする。
						enemy.attackZone.setCenter(centerBase.x + (progress - 0.5) * 200, centerBase.y);
					}
					// ▲▲▲ ステージ3のみの攻撃予告の動き ▲▲▲

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
				// 【状態3: Attacking (攻撃発生)】
				else if (enemy.state == Enemy::State::Attacking) {
					// もしガード失敗していたらプレイヤーにダメージ
					if (not enemy.isGuarding) {
						playerHP -= 40; // 敵の攻撃力
					}
					// 待機状態(Idle)に戻る
					enemy.state = Enemy::State::Idle;
					// 次の攻撃までの時間を2秒から4秒のランダムに設定
					enemy.attackTimer.set(Random(2.0, 4.0) * 1s);
					enemy.attackTimer.start();
				}
			}

			// HPが0以下の敵をリストから削除する (erase-remove idiom)
			// std::remove_if で削除対象を配列の後方に集め、その開始位置を erase に渡して実際に削除する
			enemies.erase(std::remove_if(enemies.begin(), enemies.end(), [](const Enemy& e) { return e.hp <= 0; }), enemies.end());

			// --- ステージクリア判定 ---
			if (enemies.isEmpty())
			{
				if (currentState == GameState::Stage1)
				{ // ステージ1クリア → ステージ2へ
					enemies.clear();
					// ステージ2の敵を2体追加
					enemies.push_back(Enemy{
						.pos = Scene::Center().movedBy(-200, 0),
						.initialPos = Scene::Center().movedBy(-200, 0),
						.body = Circle{ Scene::Center().movedBy(-200, 0), 50 },
						.hp = 300,
						.maxHp = 300,
						.texture = enemyTexture,
						.attackTimer = Timer{ 2s, StartImmediately::Yes },
						});
					enemies.push_back(Enemy{
						.pos = Scene::Center().movedBy(200, 0),
						.initialPos = Scene::Center().movedBy(200, 0),
						.body = Circle{ Scene::Center().movedBy(200, 0), 50 },
						.hp = 300,
						.maxHp = 300,
						.texture = enemyTexture,
						.attackTimer = Timer{ 3.5s, StartImmediately::Yes },
						});
					currentState = GameState::Stage2;
				}
				else if (currentState == GameState::Stage2)
				{ // ステージ2クリア → ステージ3へ
					enemies.clear();
					// ステージ3の敵を1体追加 (HPを少し高く設定)
					enemies.push_back(Enemy{
						.pos = Scene::Center().movedBy(-200, 0),
						.initialPos = Scene::Center().movedBy(-200, 0),
						.body = Circle{ Scene::Center().movedBy(-200, 0), 50 },
						.hp = 400,
						.maxHp = 400,
						.texture = enemyTexture,
						.attackTimer = Timer{ 1.5s, StartImmediately::Yes },
						});
					currentState = GameState::Stage3;
				}
				else if (currentState == GameState::Stage3)
				{ // ステージ3クリア → ゲームクリアへ
					currentState = GameState::Clear;
				}
			}

			// --- ゲームオーバー判定 ---
			if (playerHP <= 0)
			{
				// HPが0以下になったらゲームオーバーシーンに遷移
				currentState = GameState::GameOver;
			}
			break;

			// --- クリアシーンの処理 ---
		case GameState::Clear:
			// マウスがクリックされたらゲームを終了する
			if (MouseL.down())
			{
				System::Exit();
			}
			break;

			// --- ゲームオーバーシーンの処理 ---
		case GameState::GameOver:
			// マウスがクリックされたら
			if (MouseL.down())
			{
				// ゲームの状態をリセットしてチュートリアルに戻る
				playerHP = playerMaxHP;
				enemies.clear();
				isLongPressing = false;
				pressStopwatch.reset();
				currentState = GameState::Tutorial;
			}
			break;
		}

		// 全てのエフェクトの更新（これを毎フレーム呼ばないとエフェクトが動かない）
		effect.update();


		// ================================================================
		//  §2. 描画 (見た目)
		// ================================================================
		// 現在のシーンに応じて、描画する内容を切り替える
		switch (currentState)
		{
			// --- チュートリアルシーンの描画 ---
		case GameState::Tutorial:
			bigFont(U"操作方法").drawAt(Scene::Center().x, 100);
			font(U"マウスをクリック: 拳攻撃").drawAt(Scene::Center().x, 200);
			font(U"マウスをスライド: 縦:打撃/横:剣撃").drawAt(Scene::Center().x, 250);
			font(U"マウス長押し (1秒以上): 魔法攻撃").drawAt(Scene::Center().x, 300);
			font(U"[!]ゾーンにカーソルを合わせる: 防御").drawAt(Scene::Center().x, 350);
			font(U"\nクリックでスタート").drawAt(Scene::Center().x, 450);
			break;

			// --- ゲーム中（ステージ1, 2, 3共通）の描画 ---
		case GameState::Stage1:
		case GameState::Stage2:
		case GameState::Stage3:
			// プレイヤーHPバー
			RectF{ 10, 10, 300, 30 }.draw(Palette::Red);
			RectF{ 10, 10, (playerHP / playerMaxHP) * 300, 30 }.draw(Palette::Green);
			font(U"Player HP").drawAt(160, 25);
			{ // HP数値を描画するためのローカルスコープ
				const String hpText = U"{} / {}"_fmt(static_cast<int32>(playerHP), static_cast<int32>(playerMaxHP));
				font(hpText).draw(10 + 300 + 10, 10, Palette::White);
			}

			// 敵の描画
			for (const auto& enemy : enemies)
			{
				// 敵の頭上にHPバーを描画
				const RectF enemyHpBarArea{ Arg::center = enemy.pos.movedBy(0, -60), 120, 15 };
				enemyHpBarArea.draw(Palette::Red);
				enemyHpBarArea.stretched(-2).draw(ColorF{ 0.2 });
				const double displayHp = Max(0.0, enemy.hp); // HPが0未満にならないように
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
			if (MouseL.pressed() && not isLongPressing) { // スライドの軌跡
				Line{ mouseDownPos, Cursor::Pos() }.draw(4, Palette::Orange);
			}
			if (isLongPressing) { // 長押し攻撃の範囲
				longPressAoE.draw(ColorF{ 1.0, 0.5, 0.0, 0.5 });
			}
			break;

			// --- クリアシーンの描画 ---
		case GameState::Clear:
			bigFont(U"GAME CLEAR!").drawAt(Scene::Center(), Palette::White);
			font(U"クリックで終了").drawAt(Scene::Center().x, 400);
			break;

			// --- ゲームオーバーシーンの描画 ---
		case GameState::GameOver:
			// 背景を少し暗くして、ゲーム画面の上に重ねて表示
			Rect{ Scene::Size() }.draw(ColorF{ 0.0, 0.5 });
			bigFont(U"GAME OVER").drawAt(Scene::Center(), Palette::White);
			font(U"クリックでタイトルに戻る").drawAt(Scene::Center().x, 400);
			break;
		}
	}
}
