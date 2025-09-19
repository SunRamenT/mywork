#include <Siv3D.hpp>

struct EmojiEmitter : IEffect
{
	Vec2 m_pos;
	String m_emoji;
	const Font& m_font;

	EmojiEmitter(const Vec2& pos, const String& emoji, const Font& font)
		: m_pos{ pos }
		, m_emoji{ emoji }
		, m_font{ font } {
	}

	bool update(double t) override
	{
		const double e = EaseOutQuad(t);
		m_font(m_emoji).drawAt(m_pos.movedBy(0, -e * 40), ColorF{ 1.0, 1.0 - e });
		return (t < 0.5);
	}
};

struct Enemy
{
	Vec2 pos;
	Circle body;
	double hp;
	double maxHp;
	Timer attackTimer;
	RectF attackZone;
	bool isGuarding = false;
	enum class State { Idle, Warning, Attacking };
	State state = State::Idle;
	Texture texture;
};

enum class GameState
{
	Tutorial,
	Stage1,
	Stage2,
	Clear,
};

void Main()
{
	// --- 初期設定 & アセット準備 ---
	Window::SetTitle(U"戦闘システム - 完成版");
	Scene::SetBackground(ColorF{ 0.1, 0.1, 0.1 });
	const Font font{ 30, Typeface::Bold };
	const Font bigFont{ 50, Typeface::Bold };
	const Font effectFont{ 40, Typeface::Bold };
	const Texture enemyTexture{ U"👾"_emoji };

	// --- ゲーム全体で共有する変数 ---
	double playerHP = 200;
	const double playerMaxHP = 200;
	Vec2 mouseDownPos{ 0, 0 };
	Stopwatch pressStopwatch;
	const double longPressThreshold = 1.0;
	bool isLongPressing = false;
	Circle longPressAoE;
	Effect effect;
	GameState currentState = GameState::Tutorial;
	Array<Enemy> enemies;

	// --- メインループ ---
	while (System::Update())
	{
		// --- ロジック（更新）部分 ---
		switch (currentState)
		{
		case GameState::Tutorial:
			if (MouseL.down())
			{
				enemies.clear();
				enemies.push_back(Enemy{
					.pos = Scene::Center(),
					.body = Circle{ Scene::Center(), 50 },
					.hp = 500, .maxHp = 500,
					.attackTimer = Timer{ 3s, StartImmediately::Yes },
					.texture = enemyTexture
					});
				currentState = GameState::Stage1;
			}
			break;

		case GameState::Stage1:
		case GameState::Stage2:
			if (MouseL.down()) {
				mouseDownPos = Cursor::Pos();
				pressStopwatch.start();
				isLongPressing = false;
			}
			if (MouseL.pressed()) {
				if (pressStopwatch.sF() > longPressThreshold) {
					isLongPressing = true;
				}
				if (isLongPressing) {
					const double chargeTime = pressStopwatch.sF() - longPressThreshold;
					const double radius = 20.0 + chargeTime * 50.0;
					longPressAoE.set(Cursor::Pos(), radius);
					for (auto& enemy : enemies) {
						if (enemy.body.intersects(longPressAoE)) {
							const double damagePerSecond = 80.0;
							enemy.hp -= damagePerSecond * Scene::DeltaTime();
							if (Scene::FrameCount() % 10 == 0) {
								effect.add<EmojiEmitter>(enemy.body.center, U"🔥", effectFont);
							}
						}
					}
				}
			}
			if (MouseL.up()) {
				if (!isLongPressing) {
					const Vec2 dragVector = Cursor::Pos() - mouseDownPos;
					const double dragDistance = dragVector.length();
					if (dragDistance > 60) {
						const Line trajectory{ mouseDownPos, Cursor::Pos() };
						for (auto& enemy : enemies) {
							if (Geometry2D::Distance(enemy.body.center, trajectory) < (enemy.body.r + 20.0)) {
								double damage = (Abs(dragVector.x) > Abs(dragVector.y)) ? 70 : 50;
								enemy.hp -= damage;
								effect.add<EmojiEmitter>(enemy.body.center, U"💥", effectFont);
							}
						}
					}
					else {
						for (auto& enemy : enemies) {
							if (enemy.body.intersects(Circle{ Cursor::Pos(), 40 })) {
								enemy.hp -= 30;
								effect.add<EmojiEmitter>(enemy.body.center, U"💥", effectFont);
							}
						}
					}
				}
				pressStopwatch.reset();
				isLongPressing = false;
			}

			for (auto& enemy : enemies)
			{
				if (enemy.state == Enemy::State::Idle) {
					if (enemy.attackTimer.reachedZero()) {
						enemy.state = Enemy::State::Warning;
						const int32 dir = Random(3);
						enemy.attackZone = RectF{ Arg::center = enemy.pos.movedBy(Vec2{0, 180}.rotated(90_deg * dir)), Size{150, 150} };
						enemy.attackTimer.set(0.5s);
						enemy.attackTimer.start();
						enemy.isGuarding = false;
					}
				}
				else if (enemy.state == Enemy::State::Warning) {
					if (enemy.attackZone.mouseOver()) {
						enemy.isGuarding = true;
					}
					if (enemy.attackTimer.reachedZero()) {
						enemy.state = Enemy::State::Attacking;
					}
				}
				else if (enemy.state == Enemy::State::Attacking) {
					if (!enemy.isGuarding) {
						playerHP -= 40;
					}
					enemy.state = Enemy::State::Idle;
					enemy.attackTimer.set(Random(2.0, 4.0) * 1s);
					enemy.attackTimer.start();
				}
			}

			// HPが0以下の敵をリストから削除する (erase-removeイディオム)
			enemies.erase(std::remove_if(enemies.begin(), enemies.end(), [](const Enemy& e) { return e.hp <= 0; }), enemies.end());

			// ステージクリア条件を「敵リストが空になったら」に変更
			if (enemies.isEmpty())
			{
				if (currentState == GameState::Stage1)
				{
					enemies.clear();
					enemies.push_back(Enemy{
						.pos = Scene::Center().movedBy(-200, 0),
						.body = Circle{ Scene::Center().movedBy(-200, 0), 50 },
						.hp = 300, .maxHp = 300,
						.attackTimer = Timer{ 2s, StartImmediately::Yes },
						.texture = enemyTexture
						});
					enemies.push_back(Enemy{
						.pos = Scene::Center().movedBy(200, 0),
						.body = Circle{ Scene::Center().movedBy(200, 0), 50 },
						.hp = 300, .maxHp = 300,
						.attackTimer = Timer{ 3.5s, StartImmediately::Yes },
						.texture = enemyTexture
						});
					currentState = GameState::Stage2;
				}
				else if (currentState == GameState::Stage2)
				{
					currentState = GameState::Clear;
				}
			}
			break;
		case GameState::Clear:
			if (MouseL.down())
			{
				System::Exit();
			}
			break;
		}

		effect.update();

		// --- 描画部分 ---
		switch (currentState)
		{
		case GameState::Tutorial:
			bigFont(U"操作方法").drawAt(Scene::Center().x, 100);
			font(U"マウスをクリック: ちょい押し攻撃").drawAt(Scene::Center().x, 200);
			font(U"マウスをスライド: 軌跡攻撃 (縦/横で性能変化)").drawAt(Scene::Center().x, 250);
			font(U"マウス長押し (1秒以上): 範囲スリップ攻撃").drawAt(Scene::Center().x, 300);
			font(U"[!]ゾーンにカーソルを合わせる: ガード").drawAt(Scene::Center().x, 350);
			font(U"\nクリックでスタート").drawAt(Scene::Center().x, 450);
			break;

		case GameState::Stage1:
		case GameState::Stage2:
			// プレイヤーHPバー
			RectF{ 10, 10, 300, 30 }.draw(Palette::Red);
			RectF{ 10, 10, (playerHP / playerMaxHP) * 300, 30 }.draw(Palette::Green);
			font(U"Player HP").drawAt(160, 25);
			{
				const String hpText = U"{} / {}"_fmt(static_cast<int32>(playerHP), static_cast<int32>(playerMaxHP));
				font(hpText).draw(10 + 300 + 10, 10, Palette::White);
			}

			// 生きている敵だけを描画
			for (const auto& enemy : enemies)
			{
				const RectF enemyHpBarArea{ Arg::center = enemy.pos.movedBy(0, -60), 120, 15 };
				enemyHpBarArea.draw(Palette::Red);
				enemyHpBarArea.stretched(-2).draw(ColorF{ 0.2 });
				const double displayHp = Max(0.0, enemy.hp);
				RectF{ enemyHpBarArea.pos, enemyHpBarArea.w * (displayHp / enemy.maxHp), enemyHpBarArea.h }.draw(Palette::Limegreen);
				enemy.texture.resized(100).drawAt(enemy.pos);
				enemy.body.draw(ColorF{ 1, 0, 0, 0.3 });
				if (enemy.state == Enemy::State::Warning) {
					enemy.attackZone.draw(ColorF{ 1, 0.2, 0, 0.5 });
					font(U"!").drawAt(enemy.attackZone.center());
					if (enemy.isGuarding) {
						Circle{ Cursor::Pos(), 40 }.drawFrame(5, Palette::Cyan);
					}
				}
			}

			if (MouseL.pressed() && !isLongPressing) {
				Line{ mouseDownPos, Cursor::Pos() }.draw(4, Palette::Orange);
			}
			if (isLongPressing) {
				longPressAoE.draw(ColorF{ 1.0, 0.5, 0.0, 0.5 });
			}

			if (playerHP <= 0) {
				Rect{ Scene::Size() }.draw(ColorF{ 0.0, 0.5 });
				bigFont(U"GAME OVER").drawAt(Scene::Center(), Palette::White);
				System::Exit();
			}
			break;
		case GameState::Clear:
			bigFont(U"GAME CLEAR!").drawAt(Scene::Center(), Palette::White);
			font(U"クリックで終了").drawAt(Scene::Center().x, 400);
			break;
		}
	}
}
