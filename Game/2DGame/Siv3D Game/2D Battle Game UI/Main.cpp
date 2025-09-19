#include <Siv3D.hpp>

// ★ 変更点1: Fontを外から受け取るように修正
struct EmojiEmitter : IEffect
{
	Vec2 m_pos;
	String m_emoji;
	const Font& m_font; // Fontそのものではなく、Fontへの「参照」を持つ

	// コンストラクタでFontを受け取る
	EmojiEmitter(const Vec2& pos, const String& emoji, const Font& font)
		: m_pos{ pos }
		, m_emoji{ emoji }
		, m_font{ font } {
	}

	bool update(double t) override
	{
		// 毎フレームFontを作成するのではなく、渡されたものを使う
		const double e = EaseOutQuad(t);
		m_font(m_emoji).drawAt(m_pos.movedBy(0, -e * 40), ColorF{ 1.0, 1.0 - e });

		return (t < 0.5);
	}
};


void Main()
{
	// --- 初期設定 ---
	Window::SetTitle(U"戦闘システム - パフォーマンス改善版");
	Scene::SetBackground(ColorF{ 0.1, 0.1, 0.1 });

	// --- アセットの準備 ---
	const Font font{ 30, Typeface::Bold };
	// ★ 変更点2: エフェクト専用のFontをここで一度だけ作成
	const Font effectFont{ 40, Typeface::Bold };
	const Texture enemyTexture{ U"👾"_emoji };

	// --- ゲームの状態を管理する変数 ---
	double playerHP = 200;
	const double playerMaxHP = 200;
	double enemyHP = 500;
	const double enemyMaxHP = 500;
	Vec2 mouseDownPos{ 0, 0 };
	Stopwatch pressStopwatch;
	const double longPressThreshold = 1.0;
	const Vec2 enemyPos{ Scene::Center() };
	const Circle enemyBody{ enemyPos, 50 };
	Timer enemyAttackTimer{ 3s, StartImmediately::Yes };
	RectF enemyAttackZone;
	Stopwatch guardTimer;
	bool isGuarding = false;
	enum class EnemyState { Idle, Warning, Attacking };
	EnemyState enemyState = EnemyState::Idle;
	Effect effect;

	// --- メインループ ---
	while (System::Update())
	{
		// --- ロジック（計算・判定）部分 ---

		// 1. 敵のAIと攻撃処理
		if (enemyState == EnemyState::Idle) {
			if (enemyAttackTimer.reachedZero()) {
				enemyState = EnemyState::Warning;
				const int32 direction = Random(3);
				const Size zoneSize{ 150, 150 };
				const double offset = 180.0;
				if (direction == 0) {
					enemyAttackZone = RectF{ Arg::center = enemyPos.movedBy(0, -offset), zoneSize };
				}
				else if (direction == 1) {
					enemyAttackZone = RectF{ Arg::center = enemyPos.movedBy(0, offset), zoneSize };
				}
				else if (direction == 2) {
					enemyAttackZone = RectF{ Arg::center = enemyPos.movedBy(-offset, 0), zoneSize };
				}
				else {
					enemyAttackZone = RectF{ Arg::center = enemyPos.movedBy(offset, 0), zoneSize };
				}
				guardTimer.restart();
				isGuarding = false;
			}
		}
		else if (enemyState == EnemyState::Warning) {
			if (guardTimer.sF() <= 0.5) {
				if (enemyAttackZone.mouseOver()) {
					isGuarding = true;
				}
			}
			else {
				enemyState = EnemyState::Attacking;
			}
		}
		else if (enemyState == EnemyState::Attacking) {
			if (!isGuarding) {
				playerHP -= 40;
			}
			enemyState = EnemyState::Idle;
			enemyAttackTimer.restart();
		}

		// 2. プレイヤーの入力と攻撃処理
		if (MouseL.down()) {
			mouseDownPos = Cursor::Pos();
			pressStopwatch.start();
		}

		if (MouseL.up()) {
			const Vec2 dragVector = Cursor::Pos() - mouseDownPos;
			const double dragDistance = dragVector.length();

			if (pressStopwatch.sF() > longPressThreshold) {
				const Circle longPressAttackArea{ enemyPos.x, enemyPos.y + 100, 80 };
				if (enemyBody.intersects(longPressAttackArea)) {
					enemyHP -= 100;
					// ★ 変更点3: 作成済みのeffectFontを渡す
					effect.add<EmojiEmitter>(enemyBody.center, U"💥", effectFont);
				}
			}
			else if (dragDistance > 60) {
				const Line trajectory{ mouseDownPos, Cursor::Pos() };
				if (Geometry2D::Distance(enemyBody.center, trajectory) < (enemyBody.r + 20.0)) {
					double damage = 0;
					if (Abs(dragVector.x) > Abs(dragVector.y)) {
						damage = 70;
					}
					else {
						damage = 50;
					}
					enemyHP -= damage;
					// ★ 変更点3: 作成済みのeffectFontを渡す
					effect.add<EmojiEmitter>(enemyBody.center, U"💥", effectFont);
				}
			}
			else {
				if (enemyBody.intersects(Circle{ Cursor::Pos(), 40 })) {
					enemyHP -= 30;
					// ★ 変更点3: 作成済みのeffectFontを渡す
					effect.add<EmojiEmitter>(enemyBody.center, U"💥", effectFont);
				}
			}
			pressStopwatch.reset();
		}

		effect.update();

		// --- 描画部分 ---

		// 1. HPバーの描画
		RectF{ 10, 10, 300, 30 }.draw(Palette::Red);
		RectF{ 10, 10, (playerHP / playerMaxHP) * 300, 30 }.draw(Palette::Green);
		font(U"Player HP").drawAt(160, 25);
		RectF{ 490, 10, 300, 30 }.draw(Palette::Red);
		RectF{ 490, 10, (enemyHP / enemyMaxHP) * 300, 30 }.draw(Palette::Limegreen);
		font(U"Enemy HP").drawAt(640, 25);

		// 2. 敵の描画
		enemyTexture.resized(100).drawAt(enemyPos);
		enemyBody.draw(ColorF{ 1, 0, 0, 0.3 });

		// 3. 敵の攻撃予告を描画
		if (enemyState == EnemyState::Warning) {
			enemyAttackZone.draw(ColorF{ 1, 0.2, 0, 0.5 });
			font(U"!").drawAt(enemyAttackZone.center());
			if (isGuarding) {
				Circle{ Cursor::Pos(), 40 }.drawFrame(5, Palette::Cyan);
			}
		}

		// 4. 軌跡の描画
		if (MouseL.pressed()) {
			Line{ mouseDownPos, Cursor::Pos() }.draw(4, Palette::Orange);
		}

		// 5. ゲームオーバーとクリアの表示
		if (playerHP <= 0) {
			Rect{ Scene::Size() }.draw(ColorF{ 0.0, 0.5 });
			font(U"GAME OVER").drawAt(Scene::Center(), Palette::White);
			System::Exit();
		}
		if (enemyHP <= 0) {
			Rect{ Scene::Size() }.draw(ColorF{ 0.0, 0.5 });
			font(U"CLEAR!").drawAt(Scene::Center(), Palette::White);
			System::Exit();
		}
	}
}
