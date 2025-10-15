// DXApplication.cpp (プレイヤー対応版)

#include "DXApplication.h"
#include "MazeGenerator.h"
#include <random>
#include <algorithm>
#include <string>
#include <sstream>
#include <iomanip>
#include <chrono>
#include <thread>
#include <windows.h>

DXApplication::DXApplication(unsigned int width, unsigned int height, std::wstring title)
	: hwnd_(nullptr)
	, title_(title)
	, windowWidth_(width)
	, windowHeight_(height)
	, viewport_(0.0f, 0.0f, static_cast<float>(width), static_cast<float>(height))
	, scissorrect_(0, 0, static_cast<LONG>(width), static_cast<LONG>(height))
	, vertexBufferView_({})
	, indexBufferView_({})
	, instanceBufferView_({})
	, instanceCount_(0)
	, pCbvDataBegin_(nullptr)
	, pInstanceDataBegin_(nullptr)
	, moveCooldown_(0.0f)
	, currentScene_(GameScene::Title) //初期シーンをタイトルに
	, timeLimit_(0.0f)
	, fenceValue_(0)
	, fenceEvent_(nullptr)
{
}

DXApplication::~DXApplication()
{
	WaitForGpu();
	CloseHandle(fenceEvent_);
	if (hudHwnd_) {
		DestroyWindow(hudHwnd_); // HUDウィンドウを破棄
	}
}

void DXApplication::OnInit(HWND hwnd)
{
	hwnd_ = hwnd; // ウィンドウハンドルを保持
	LoadPipeline(hwnd);
	LoadAssets();
	// ResetGameはゲーム開始時に呼ぶので、ここでは呼ばない
	// 1. メインウィンドウと同じ位置・サイズを取得
	RECT rect;
	GetWindowRect(hwnd_, &rect);

	// 2. HUD用のウィンドウクラスを登録 (もしメインと違うなら)
	//    メインと同じウィンドウクラスを使うならこの部分は不要
	WNDCLASS wc = {};
	wc.lpfnWndProc = DefWindowProc; // 何も処理しないプロシージャ
	wc.lpszClassName = L"HUDWindowClass";
	wc.hInstance = GetModuleHandle(NULL);
	RegisterClass(&wc);

	// 3. 透明＆クリック透過のスタイルでウィンドウを作成
	hudHwnd_ = CreateWindowEx(
		WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST, // ★重要なスタイル
		L"HUDWindowClass",    // 上で登録したクラス名
		L"HUD",               // ウィンドウタイトル（表示されない）
		WS_POPUP,             // 枠やタイトルバーなし
		rect.left, rect.top,
		rect.right - rect.left, rect.bottom - rect.top,
		hwnd_,                // オーナーをメインウィンドウに設定
		NULL, GetModuleHandle(NULL), NULL
	);

	// 4. 黒(RGB(0,0,0))を透明色に設定
	SetLayeredWindowAttributes(hudHwnd_, RGB(0, 0, 0), 0, LWA_COLORKEY);

	// 5. HUDウィンドウを表示
	ShowWindow(hudHwnd_, SW_SHOW);
}

void DXApplication::OnClick()
{
	switch (currentScene_)
	{
	case GameScene::Title:
		ResetGame();
		currentScene_ = GameScene::InGame;
		break;
	case GameScene::GameClear:
	case GameScene::GameOver:
		currentScene_ = GameScene::Title;
		break;
	default:
		// ゲーム中はクリックで何もしない
		break;
	}
}

void DXApplication::OnUpdate()
{
	// HUDウィンドウの位置をメインウィンドウに同期
	RECT rect;
	GetWindowRect(hwnd_, &rect);
	SetWindowPos(hudHwnd_, NULL, rect.left, rect.top, 0, 0, SWP_NOSIZE | SWP_NOZORDER);

	// シーンごとに適切な更新関数を呼び出す
	switch (currentScene_)
	{
	case GameScene::Title:
		UpdateTitle();
		break;
	case GameScene::InGame:
		UpdateInGame();
		break;
	case GameScene::GameClear:
	case GameScene::GameOver:
		UpdateResult();
		break;
	}
}

void DXApplication::OnRender()
{
	// 1. DirectXの3D描画準備と実行
	ThrowIfFailed(commandAllocator_->Reset());
	ThrowIfFailed(commandList_->Reset(commandAllocator_.Get(), pipelinestate_.Get()));

	auto frameIndex = swapchain_->GetCurrentBackBufferIndex();
	auto barrierToRenderTarget = CD3DX12_RESOURCE_BARRIER::Transition(renderTargets_[frameIndex].Get(), D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATE_RENDER_TARGET);
	commandList_->ResourceBarrier(1, &barrierToRenderTarget);

	CD3DX12_CPU_DESCRIPTOR_HANDLE rtvHandle(rtvHeaps_->GetCPUDescriptorHandleForHeapStart(), frameIndex, device_->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV));
	const float clearColor[] = { 0.0f, 0.2f, 0.4f, 1.0f };

	commandList_->ClearRenderTargetView(rtvHandle, clearColor, 0, nullptr);
	commandList_->OMSetRenderTargets(1, &rtvHandle, true, nullptr);

	// InGameの時だけ3Dオブジェクトを描画
	if (currentScene_ == GameScene::InGame)
	{
		RenderInGame();
	}

	auto barrierToPresent = CD3DX12_RESOURCE_BARRIER::Transition(renderTargets_[frameIndex].Get(), D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATE_PRESENT);
	commandList_->ResourceBarrier(1, &barrierToPresent);
	ThrowIfFailed(commandList_->Close());
	ID3D12CommandList* commandLists[] = { commandList_.Get() };
	commandQueue_->ExecuteCommandLists(1, commandLists);

	// 2. DirectXのフレームを画面に表示
	ThrowIfFailed(swapchain_->Present(1, 0));

	// 3. GDIを使ってテキストを描画 (画面表示の後)
	switch (currentScene_)
	{
	case GameScene::Title:
		RenderTitle(); // コメントアウトを解除！
		break;
	case GameScene::GameClear:
	case GameScene::GameOver:
		RenderResult(); // リザルト画面の描画も追加
		break;
	default:
		// InGameではテキストを描画しない
		RenderInGameUI(); // InGame UIの描画を追加
		break;
	}

	// 4. GPUの完了を待つ
	WaitForGpu();
}

// DXApplication.cpp の ResetGame関数をまるごと置き換え
void DXApplication::ResetGame()
{
	// プレイヤーとゴールの位置を設定
	playerGridPos_ = { 1, 1 };
	goalGridPos_ = { MazeWidth - 2, MazeHeight - 2 };
	mazeData_ = MazeGenerator::Generate(MazeWidth, MazeHeight, playerGridPos_, goalGridPos_);

	// --- ここから鍵の配置ロジックを変更 ---

	// 1. 通れる床の座標リストを作成する
	std::vector<Vector2Int> floorTiles;
	for (int y = 1; y < MazeHeight - 1; ++y) {
		for (int x = 1; x < MazeWidth - 1; ++x) {
			// 床(1)であり、プレイヤーとゴールの初期位置ではない場所をリストアップ
			if (mazeData_[y][x] == 1 &&
				!(x == playerGridPos_.x && y == playerGridPos_.y) &&
				!(x == goalGridPos_.x && y == goalGridPos_.y))
			{
				floorTiles.push_back({ x, y });
			}
		}
	}

	// 2. 床リストをシャッフルして、ランダムな配置場所を作る
	std::random_device rd;
	std::mt19937 g(rd());
	std::shuffle(floorTiles.begin(), floorTiles.end(), g);

	// 3. シャッフルされたリストの先頭から2つを選んでキーを配置
	keys_.clear();
	if (floorTiles.size() >= 2) { // 念のため、置ける場所が2つ以上あるかチェック
		keys_.push_back({ floorTiles[0], true });
		keys_.push_back({ floorTiles[1], true });
	}

	// --- ここまで変更 ---

	goalActive_ = false;

	// 制限時間を設定
	timeLimit_ = 60.0f; // 60秒

	// インスタンスデータを再構築
	std::vector<InstanceData> instances;
	for (int y = 0; y < MazeHeight; ++y) {
		for (int x = 0; x < MazeWidth; ++x) {
			if (mazeData_[y][x] == 1) { // 床
				instances.push_back({ { (float)(x - MazeWidth / 2), (float)(y - MazeHeight / 2) * -1.0f, 0.0f }, { 1.0f, 1.0f, 1.0f, 1.0f } });
			}
		}
	}
	instances.push_back({ { (float)(goalGridPos_.x - MazeWidth / 2), (float)(goalGridPos_.y - MazeHeight / 2) * -1.0f, 0.0f }, { 0.5f, 0.5f, 0.5f, 1.0f } }); // ゴール
	for (const auto& key : keys_) { // キー
		instances.push_back({ { (float)(key.gridPos.x - MazeWidth / 2), (float)(key.gridPos.y - MazeHeight / 2) * -1.0f, 0.0f }, { 1.0f, 0.8f, 0.0f, 1.0f } });
	}
	instances.push_back({ { (float)(playerGridPos_.x - MazeWidth / 2), (float)(playerGridPos_.y - MazeHeight / 2) * -1.0f, 0.0f }, { 1.0f, 0.0f, 0.0f, 1.0f } }); // プレイヤー

	instanceCount_ = static_cast<UINT>(instances.size());
	const UINT bufferSize = instanceCount_ * sizeof(InstanceData);
	memcpy(pInstanceDataBegin_, instances.data(), bufferSize);
}

void DXApplication::UpdateTitle()
{
	// Enterキーが押されたら、OnClick()関数を呼び出す
	if (GetAsyncKeyState(VK_RETURN) & 0x8000) {
		OnClick();
	}
}

void DXApplication::UpdateInGame()
{
	// 制限時間を減らす
	timeLimit_ -= 1.0f / 60.0f; // 60FPS想定
	if (timeLimit_ <= 0.0f) {
		timeLimit_ = 0.0f;
		currentScene_ = GameScene::GameOver;
		return;
	}

	// プレイヤーの移動処理
	const float moveInterval = 0.1f;
	if (moveCooldown_ > 0.0f) {
		moveCooldown_ -= 1.0f / 60.0f;
	}
	if (moveCooldown_ <= 0.0f) {
		bool moved = false;
		if (GetAsyncKeyState('W') & 0x8000) { MovePlayer({ 0, -1 }); moved = true; }
		else if (GetAsyncKeyState('S') & 0x8000) { MovePlayer({ 0,  1 }); moved = true; }
		else if (GetAsyncKeyState('A') & 0x8000) { MovePlayer({ -1, 0 }); moved = true; }
		else if (GetAsyncKeyState('D') & 0x8000) { MovePlayer({ 1, 0 }); moved = true; }
		if (moved) {
			moveCooldown_ = moveInterval;
		}
	}
}

void DXApplication::UpdateResult()
{
	// Enterキーが押されたら、OnClick()関数を呼び出す
	if (GetAsyncKeyState(VK_RETURN) & 0x8000) {
		OnClick();
	}
}

// DXApplication.cpp

void DXApplication::RenderTitle()
{
	HDC hdc = GetDC(hudHwnd_);

	RECT clientRect;
	GetClientRect(hudHwnd_, &clientRect);
	int width = clientRect.right - clientRect.left;
	int height = clientRect.bottom - clientRect.top;

	// ダブルバッファリングの準備
	HDC memDC = CreateCompatibleDC(hdc);
	HBITMAP hBitmap = CreateCompatibleBitmap(hdc, width, height);
	HBITMAP oldBitmap = (HBITMAP)SelectObject(memDC, hBitmap);

	// 背景をクリア
	HBRUSH hBrush = CreateSolidBrush(RGB(0, 0, 0));
	FillRect(memDC, &clientRect, hBrush);
	DeleteObject(hBrush);

	// 共通の設定
	SetTextColor(memDC, RGB(255, 255, 255));
	SetBkMode(memDC, TRANSPARENT);

	// 1. タイトル用の大きなフォントを作成・描画
	HFONT hTitleFont = CreateFont(100, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, SHIFTJIS_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Meiryo");
	HFONT oldFont = (HFONT)SelectObject(memDC, hTitleFont); // 古いフォントを保持

	TextOut(memDC, windowWidth_ / 2 - 180, windowHeight_ / 2 - 40, L"MAZE GAME", 9);

	// 2. 指示用の小さなフォントを作成・描画
	HFONT hInstructionFont = CreateFont(32, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, SHIFTJIS_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Meiryo");
	SelectObject(memDC, hInstructionFont); // フォントを指示用に切り替え

	TextOut(memDC, windowWidth_ / 2 - 42, windowHeight_ / 2 + 50, L"Click to Start", 14);

	// --- 変更点ここまで ---

	// 完成した絵をウィンドウへ転送
	BitBlt(hdc, 0, 0, width, height, memDC, 0, 0, SRCCOPY);

	// 後片付け
	SelectObject(memDC, oldFont);     // 最初に保持した古いフォントに戻す
	DeleteObject(hTitleFont);         // ★タイトル用フォントを削除
	DeleteObject(hInstructionFont); // ★指示用フォントを削除

	SelectObject(memDC, oldBitmap);
	DeleteObject(hBitmap);
	DeleteDC(memDC);

	ReleaseDC(hudHwnd_, hdc);
}

void DXApplication::RenderResult()
{
	HDC hdc = GetDC(hudHwnd_);

	RECT clientRect;
	GetClientRect(hudHwnd_, &clientRect);
	int width = clientRect.right - clientRect.left;
	int height = clientRect.bottom - clientRect.top;

	// ダブルバッファリングの準備
	HDC memDC = CreateCompatibleDC(hdc);
	HBITMAP hBitmap = CreateCompatibleBitmap(hdc, width, height);
	HBITMAP oldBitmap = (HBITMAP)SelectObject(memDC, hBitmap);

	// 背景をクリア
	HBRUSH hBrush = CreateSolidBrush(RGB(0, 0, 0));
	FillRect(memDC, &clientRect, hBrush);
	DeleteObject(hBrush);

	// 共通の設定
	SetTextColor(memDC, RGB(255, 255, 255));
	SetBkMode(memDC, TRANSPARENT);

	// --- ★★★ ここからロジックを修正 ★★★ ---

	// 1. 結果表示用の大きなフォントを作成・選択・描画
	HFONT hResultFont = CreateFont(60, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, SHIFTJIS_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Meiryo");
	HFONT oldFont = (HFONT)SelectObject(memDC, hResultFont); // 古いフォントを保持

	if (currentScene_ == GameScene::GameClear) {
		// 中央揃えのために文字サイズを考慮
		const WCHAR* text = L"GAME CLEAR!";
		SIZE textSize;
		GetTextExtentPoint32(memDC, text, wcslen(text), &textSize);
		TextOut(memDC, (windowWidth_ - textSize.cx) / 2, windowHeight_ / 2 - 40, text, wcslen(text));
	}
	else {
		const WCHAR* text = L"GAME OVER";
		SIZE textSize;
		GetTextExtentPoint32(memDC, text, wcslen(text), &textSize);
		TextOut(memDC, (windowWidth_ - textSize.cx) / 2, windowHeight_ / 2 - 40, text, wcslen(text));
	}

	// 2. 指示用の小さなフォントを作成・選択・描画
	HFONT hInstructionFont = CreateFont(32, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, SHIFTJIS_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Meiryo");
	SelectObject(memDC, hInstructionFont); // フォントを指示用に切り替え

	const WCHAR* text = L"Click to Title";
	SIZE textSize;
	GetTextExtentPoint32(memDC, text, wcslen(text), &textSize);
	TextOut(memDC, (windowWidth_ - textSize.cx) / 2, windowHeight_ / 2 + 30, text, wcslen(text));

	// --- 描画完了 ---

	// 完成した絵をウィンドウへ転送
	BitBlt(hdc, 0, 0, width, height, memDC, 0, 0, SRCCOPY);

	// 後片付け
	SelectObject(memDC, oldFont);     // 最初に保持した古いフォントに戻す
	DeleteObject(hResultFont);        // ★結果表示用フォントを削除
	DeleteObject(hInstructionFont);   // ★指示用フォントを削除

	SelectObject(memDC, oldBitmap);
	DeleteObject(hBitmap);
	DeleteDC(memDC);

	ReleaseDC(hudHwnd_, hdc);
}

void DXApplication::RenderInGame()
{
	// 迷路の描画命令だけをここに残す
	commandList_->SetPipelineState(pipelinestate_.Get());
	commandList_->SetGraphicsRootSignature(rootsignature_.Get());
	ID3D12DescriptorHeap* ppHeaps[] = { cbvHeap_.Get() };
	commandList_->SetDescriptorHeaps(_countof(ppHeaps), ppHeaps);
	commandList_->SetGraphicsRootDescriptorTable(0, cbvHeap_->GetGPUDescriptorHandleForHeapStart());
	commandList_->RSSetViewports(1, &viewport_);
	commandList_->RSSetScissorRects(1, &scissorrect_);
	commandList_->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
	D3D12_VERTEX_BUFFER_VIEW views[] = { vertexBufferView_, instanceBufferView_ };
	commandList_->IASetVertexBuffers(0, 2, views);
	commandList_->IASetIndexBuffer(&indexBufferView_);
	commandList_->DrawIndexedInstanced(6, instanceCount_, 0, 0, 0);
}

// DXApplication.cpp

void DXApplication::RenderInGameUI()
{
	HDC hdc = GetDC(hudHwnd_);

	RECT clientRect;
	GetClientRect(hudHwnd_, &clientRect);
	int width = clientRect.right - clientRect.left;
	int height = clientRect.bottom - clientRect.top;

	// 1. メモリデバイスコンテキスト(DC)を作成
	HDC memDC = CreateCompatibleDC(hdc);

	// 2. メモリDCに書き込むためのビットマップを作成
	HBITMAP hBitmap = CreateCompatibleBitmap(hdc, width, height);

	// 3. 作成したビットマップをメモリDCに選択
	HBITMAP oldBitmap = (HBITMAP)SelectObject(memDC, hBitmap);

	// --- ここから下の描画処理は、すべてメモリDC(memDC)に対して行う ---

	// 背景を透明色(黒)でクリア
	HBRUSH hBrush = CreateSolidBrush(RGB(0, 0, 0));
	FillRect(memDC, &clientRect, hBrush);
	DeleteObject(hBrush);

	// フォント設定
	HFONT hFont = CreateFont(28, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, SHIFTJIS_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Meiryo");
	HFONT oldFont = (HFONT)SelectObject(memDC, hFont);
	SetTextColor(memDC, RGB(255, 255, 255));
	SetBkMode(memDC, TRANSPARENT);

	// 時間の文字列を描画
	WCHAR timerStr[32];
	swprintf_s(timerStr, L"Time: %.1f", timeLimit_);
	TextOut(memDC, 10, 30, timerStr, wcslen(timerStr));

	// 1. 残りのカギの数を数える
	int remainingKeys = 0;
	for (const auto& key : keys_) {
		if (key.isActive) {
			remainingKeys++;
		}
	}

	// 2. カギの数を文字列に変換
	WCHAR keysStr[32];
	swprintf_s(keysStr, L"Key残り: %d", remainingKeys);

	// 3. カギの数を描画 (タイマーの下)
	TextOut(memDC, 10, 70, keysStr, wcslen(keysStr));

	// --- 描画完了 ---

	// 4. 完成した絵を、メモリDCからウィンドウのDCへ一気に転送
	BitBlt(hdc, 0, 0, width, height, memDC, 0, 0, SRCCOPY);

	// 5. 後片付け
	SelectObject(memDC, oldFont);
	DeleteObject(hFont);
	SelectObject(memDC, oldBitmap);
	DeleteObject(hBitmap);
	DeleteDC(memDC);

	ReleaseDC(hudHwnd_, hdc);
}

void DXApplication::OnDestroy()
{
	// 処理はデストラクタに移動
}

void DXApplication::WaitForGpu()
{
	ThrowIfFailed(commandQueue_->Signal(fence_.Get(), ++fenceValue_));
	if (fence_->GetCompletedValue() < fenceValue_)
	{
		ThrowIfFailed(fence_->SetEventOnCompletion(fenceValue_, fenceEvent_));
		WaitForSingleObject(fenceEvent_, INFINITE);
	}
}

//プレイヤー移動関数を実装

void DXApplication::MovePlayer(Vector2Int direction)
{
	Vector2Int targetPos = { playerGridPos_.x + direction.x, playerGridPos_.y + direction.y };
	if (targetPos.x < 0 || targetPos.x >= MazeWidth || targetPos.y < 0 || targetPos.y >= MazeHeight) return;
	if (mazeData_[targetPos.y][targetPos.x] == 0) return;
	playerGridPos_ = targetPos;

	bool allKeysCollected = true;
	for (auto& key : keys_) {
		if (key.isActive && key.gridPos.x == playerGridPos_.x && key.gridPos.y == playerGridPos_.y) {
			key.isActive = false;
		}
		if (key.isActive) {
			allKeysCollected = false;
		}
	}
	if (allKeysCollected && !goalActive_) {
		goalActive_ = true;
	}
	if (goalActive_ && playerGridPos_.x == goalGridPos_.x && playerGridPos_.y == goalGridPos_.y) {
		currentScene_ = GameScene::GameClear; // ゲームクリアシーンに遷移
		return; // 移動処理を中断
	}

	if (pInstanceDataBegin_) {
		InstanceData* pInstanceData = reinterpret_cast<InstanceData*>(pInstanceDataBegin_);
		int floorInstanceCount = instanceCount_ - keys_.size() - 2;
		int goalIndex = floorInstanceCount;
		int playerIndex = instanceCount_ - 1;
		pInstanceData[goalIndex].color = goalActive_ ? DirectX::XMFLOAT4(0.0f, 1.0f, 0.0f, 1.0f) : DirectX::XMFLOAT4(0.5f, 0.5f, 0.5f, 1.0f);
		for (size_t i = 0; i < keys_.size(); ++i) {
			int keyIndex = floorInstanceCount + 1 + i;
			if (!keys_[i].isActive) {
				pInstanceData[keyIndex].position = DirectX::XMFLOAT3(1000.0f, 1000.0f, 1000.0f);
			}
		}
		pInstanceData[playerIndex].position = DirectX::XMFLOAT3((float)(playerGridPos_.x - MazeWidth / 2), (float)(playerGridPos_.y - MazeHeight / 2) * -1.0f, 0.0f);
	}
}

void DXApplication::LoadPipeline(HWND hwnd)
{
	// (この関数の中身は変更なし)
	UINT dxgiFactoryFlags = 0;
#if defined(_DEBUG)
	ComPtr<ID3D12Debug> debugLayer;
	if (SUCCEEDED(D3D12GetDebugInterface(IID_PPV_ARGS(&debugLayer)))) {
		debugLayer->EnableDebugLayer();
		dxgiFactoryFlags |= DXGI_CREATE_FACTORY_DEBUG;
	}
#endif
	ComPtr<IDXGIFactory6> dxgiFactory;
	ThrowIfFailed(CreateDXGIFactory2(dxgiFactoryFlags, IID_PPV_ARGS(&dxgiFactory)));
	CreateD3D12Device(dxgiFactory.Get(), device_.ReleaseAndGetAddressOf());
	D3D12_COMMAND_QUEUE_DESC cqDesc = {};
	ThrowIfFailed(device_->CreateCommandQueue(&cqDesc, IID_PPV_ARGS(commandQueue_.GetAddressOf())));
	ThrowIfFailed(device_->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(commandAllocator_.GetAddressOf())));
	ThrowIfFailed(device_->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, commandAllocator_.Get(), nullptr, IID_PPV_ARGS(commandList_.GetAddressOf())));
	ThrowIfFailed(commandList_->Close());
	DXGI_SWAP_CHAIN_DESC1 scDesc = {};
	scDesc.BufferCount = kFrameCount;
	scDesc.Width = windowWidth_;
	scDesc.Height = windowHeight_;
	scDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
	scDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
	scDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
	scDesc.SampleDesc.Count = 1;
	ComPtr<IDXGISwapChain1> swapchain1;
	ThrowIfFailed(dxgiFactory->CreateSwapChainForHwnd(commandQueue_.Get(), hwnd, &scDesc, nullptr, nullptr, &swapchain1));
	ThrowIfFailed(swapchain1.As(&swapchain_));
	D3D12_DESCRIPTOR_HEAP_DESC rtvHeapDesc = {};
	rtvHeapDesc.NumDescriptors = kFrameCount;
	rtvHeapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
	ThrowIfFailed(device_->CreateDescriptorHeap(&rtvHeapDesc, IID_PPV_ARGS(rtvHeaps_.GetAddressOf())));
	CD3DX12_CPU_DESCRIPTOR_HANDLE rtvHandle(rtvHeaps_->GetCPUDescriptorHandleForHeapStart());
	for (UINT i = 0; i < kFrameCount; i++) {
		ThrowIfFailed(swapchain_->GetBuffer(i, IID_PPV_ARGS(renderTargets_[i].GetAddressOf())));
		device_->CreateRenderTargetView(renderTargets_[i].Get(), nullptr, rtvHandle);
		rtvHandle.Offset(1, device_->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV));
	}
	ThrowIfFailed(device_->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(fence_.GetAddressOf())));
	fenceValue_ = 1;
	fenceEvent_ = CreateEvent(nullptr, FALSE, FALSE, nullptr);
	WaitForGpu();
}

void DXApplication::LoadAssets()
{
	// ルートシグネチャの生成
	{
		CD3DX12_DESCRIPTOR_RANGE1 ranges[1];
		ranges[0].Init(D3D12_DESCRIPTOR_RANGE_TYPE_CBV, 1, 0, 0, D3D12_DESCRIPTOR_RANGE_FLAG_DATA_STATIC);
		CD3DX12_ROOT_PARAMETER1 rootParameters[1];
		rootParameters[0].InitAsDescriptorTable(1, &ranges[0], D3D12_SHADER_VISIBILITY_VERTEX);
		D3D12_ROOT_SIGNATURE_DESC1 rootSignatureDesc = {};
		rootSignatureDesc.NumParameters = _countof(rootParameters);
		rootSignatureDesc.pParameters = rootParameters;
		rootSignatureDesc.Flags = D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT;
		ComPtr<ID3DBlob> signature, error;
		D3D12_VERSIONED_ROOT_SIGNATURE_DESC versionedDesc = {};
		versionedDesc.Version = D3D_ROOT_SIGNATURE_VERSION_1_1;
		versionedDesc.Desc_1_1 = rootSignatureDesc;
		ThrowIfFailed(D3D12SerializeVersionedRootSignature(&versionedDesc, &signature, &error));
		ThrowIfFailed(device_->CreateRootSignature(0, signature->GetBufferPointer(), signature->GetBufferSize(), IID_PPV_ARGS(rootsignature_.GetAddressOf())));
	}

	// パイプラインステート (シェーダーと入力レイアウト)
	{
		ComPtr<ID3DBlob> vsBlob, psBlob;
#if defined(_DEBUG)
		UINT compileFlags = D3DCOMPILE_DEBUG | D3DCOMPILE_SKIP_OPTIMIZATION;
#else
		UINT compileFlags = 0;
#endif
		ThrowIfFailed(D3DCompileFromFile(L"BasicVertexShader.hlsl", nullptr, nullptr, "BasicVS", "vs_5_0", compileFlags, 0, &vsBlob, nullptr));
		ThrowIfFailed(D3DCompileFromFile(L"BasicPixelShader.hlsl", nullptr, nullptr, "BasicPS", "ps_5_0", compileFlags, 0, &psBlob, nullptr));
		D3D12_INPUT_ELEMENT_DESC inputElementDescs[] = {
			{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
			{ "INSTANCE_POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 1, 0, D3D12_INPUT_CLASSIFICATION_PER_INSTANCE_DATA, 1 },
			{ "INSTANCE_COLOR", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 1, 12, D3D12_INPUT_CLASSIFICATION_PER_INSTANCE_DATA, 1 }
		};
		D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = {};
		psoDesc.pRootSignature = rootsignature_.Get();
		psoDesc.InputLayout = { inputElementDescs, _countof(inputElementDescs) };
		psoDesc.VS = CD3DX12_SHADER_BYTECODE(vsBlob.Get());
		psoDesc.PS = CD3DX12_SHADER_BYTECODE(psBlob.Get());
		psoDesc.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_DEFAULT);
		psoDesc.BlendState = CD3DX12_BLEND_DESC(D3D12_DEFAULT);
		psoDesc.DepthStencilState.DepthEnable = FALSE;
		psoDesc.DepthStencilState.StencilEnable = FALSE;
		psoDesc.SampleMask = UINT_MAX;
		psoDesc.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
		psoDesc.NumRenderTargets = 1;
		psoDesc.RTVFormats[0] = DXGI_FORMAT_R8G8B8A8_UNORM;
		psoDesc.SampleDesc.Count = 1;
		ThrowIfFailed(device_->CreateGraphicsPipelineState(&psoDesc, IID_PPV_ARGS(pipelinestate_.GetAddressOf())));
	}

	// 頂点/インデックスバッファ (四角形1つ分)
	{
		DirectX::XMFLOAT3 vertices[] = { { -0.5f, -0.5f, 0.0f }, { -0.5f, 0.5f, 0.0f }, { 0.5f, -0.5f, 0.0f }, { 0.5f, 0.5f, 0.0f } };
		const UINT vertexBufferSize = sizeof(vertices);
		unsigned short indices[] = { 0, 1, 2, 2, 1, 3 };
		const UINT indexBufferSize = sizeof(indices);
		auto heapProps = CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_UPLOAD);
		auto vbResourceDesc = CD3DX12_RESOURCE_DESC::Buffer(vertexBufferSize);
		ThrowIfFailed(device_->CreateCommittedResource(&heapProps, D3D12_HEAP_FLAG_NONE, &vbResourceDesc, D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(vertexBuffer_.GetAddressOf())));
		auto ibResourceDesc = CD3DX12_RESOURCE_DESC::Buffer(indexBufferSize);
		ThrowIfFailed(device_->CreateCommittedResource(&heapProps, D3D12_HEAP_FLAG_NONE, &ibResourceDesc, D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(indexBuffer_.GetAddressOf())));
		UINT8* pVertexDataBegin, * pIndexDataBegin;
		CD3DX12_RANGE readRange(0, 0);
		ThrowIfFailed(vertexBuffer_->Map(0, &readRange, reinterpret_cast<void**>(&pVertexDataBegin)));
		memcpy(pVertexDataBegin, vertices, vertexBufferSize);
		vertexBuffer_->Unmap(0, nullptr);
		ThrowIfFailed(indexBuffer_->Map(0, &readRange, reinterpret_cast<void**>(&pIndexDataBegin)));
		memcpy(pIndexDataBegin, indices, indexBufferSize);
		indexBuffer_->Unmap(0, nullptr);
		vertexBufferView_.BufferLocation = vertexBuffer_->GetGPUVirtualAddress();
		vertexBufferView_.StrideInBytes = sizeof(DirectX::XMFLOAT3);
		vertexBufferView_.SizeInBytes = vertexBufferSize;
		indexBufferView_.BufferLocation = indexBuffer_->GetGPUVirtualAddress();
		indexBufferView_.SizeInBytes = indexBufferSize;
		indexBufferView_.Format = DXGI_FORMAT_R16_UINT;
	}

	//迷路とプレイヤーのインスタンスバッファを作成
	{
		// --- 1. 迷路とプレイヤーの初期位置を設定 ---
		playerGridPos_ = { 1, 1 };
		goalGridPos_ = { MazeWidth - 2, MazeHeight - 2 };
		mazeData_ = MazeGenerator::Generate(MazeWidth, MazeHeight, playerGridPos_, goalGridPos_);

		// --- 2. キーの配置 ---
		keys_.clear();
		keys_.push_back({ { MazeWidth / 2, 1 }, true });
		keys_.push_back({ { 1, MazeHeight - 2 }, true });

		goalActive_ = false;

		// --- 3. インスタンスデータを作成 ---
		std::vector<InstanceData> instances;

		// 床
		for (int y = 0; y < MazeHeight; ++y) {
			for (int x = 0; x < MazeWidth; ++x) {
				if (mazeData_[y][x] == 1) {
					InstanceData data;
					data.position = DirectX::XMFLOAT3((float)(x - MazeWidth / 2), (float)(y - MazeHeight / 2) * -1.0f, 0.0f);
					data.color = DirectX::XMFLOAT4(1.0f, 1.0f, 1.0f, 1.0f);
					instances.push_back(data);
				}
			}
		}

		// ゴール
		InstanceData goalData;
		goalData.position = DirectX::XMFLOAT3((float)(goalGridPos_.x - MazeWidth / 2), (float)(goalGridPos_.y - MazeHeight / 2) * -1.0f, 0.0f);
		goalData.color = DirectX::XMFLOAT4(0.5f, 0.5f, 0.5f, 1.0f);
		instances.push_back(goalData);

		// キー
		for (const auto& key : keys_) {
			InstanceData keyData;
			keyData.position = DirectX::XMFLOAT3((float)(key.gridPos.x - MazeWidth / 2), (float)(key.gridPos.y - MazeHeight / 2) * -1.0f, 0.0f);
			keyData.color = DirectX::XMFLOAT4(1.0f, 0.8f, 0.0f, 1.0f);
			instances.push_back(keyData);
		}

		// プレイヤー
		InstanceData playerData;
		playerData.position = DirectX::XMFLOAT3((float)(playerGridPos_.x - MazeWidth / 2), (float)(playerGridPos_.y - MazeHeight / 2) * -1.0f, 0.0f);
		playerData.color = DirectX::XMFLOAT4(1.0f, 0.0f, 0.0f, 1.0f);
		instances.push_back(playerData);

		instanceCount_ = static_cast<UINT>(instances.size());
		const UINT bufferSize = instanceCount_ * sizeof(InstanceData);

		auto heapProps = CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_UPLOAD);
		auto resourceDesc = CD3DX12_RESOURCE_DESC::Buffer(bufferSize);
		ThrowIfFailed(device_->CreateCommittedResource(&heapProps, D3D12_HEAP_FLAG_NONE, &resourceDesc, D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(instanceBuffer_.GetAddressOf())));

		//バッファをマップしたままにして、ポインタを保持する
		CD3DX12_RANGE readRange(0, 0);
		ThrowIfFailed(instanceBuffer_->Map(0, &readRange, reinterpret_cast<void**>(&pInstanceDataBegin_)));
		memcpy(pInstanceDataBegin_, instances.data(), bufferSize);
		// Unmapしない！ 

		instanceBufferView_.BufferLocation = instanceBuffer_->GetGPUVirtualAddress();
		instanceBufferView_.StrideInBytes = sizeof(InstanceData);
		instanceBufferView_.SizeInBytes = bufferSize;
	}

	// 定数バッファの作成
	{
		D3D12_DESCRIPTOR_HEAP_DESC cbvHeapDesc = {};
		cbvHeapDesc.NumDescriptors = 1;
		cbvHeapDesc.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE;
		cbvHeapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV;
		ThrowIfFailed(device_->CreateDescriptorHeap(&cbvHeapDesc, IID_PPV_ARGS(&cbvHeap_)));
		auto heapProps = CD3DX12_HEAP_PROPERTIES(D3D12_HEAP_TYPE_UPLOAD);
		auto resourceDesc = CD3DX12_RESOURCE_DESC::Buffer((sizeof(DirectX::XMMATRIX) + 255) & ~255);
		ThrowIfFailed(device_->CreateCommittedResource(&heapProps, D3D12_HEAP_FLAG_NONE, &resourceDesc, D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(&constantBuffer_)));
		D3D12_CONSTANT_BUFFER_VIEW_DESC cbvDesc = {};
		cbvDesc.BufferLocation = constantBuffer_->GetGPUVirtualAddress();
		cbvDesc.SizeInBytes = (sizeof(DirectX::XMMATRIX) + 255) & ~255;
		device_->CreateConstantBufferView(&cbvDesc, cbvHeap_->GetCPUDescriptorHandleForHeapStart());

		float aspectRatio = static_cast<float>(windowWidth_) / static_cast<float>(windowHeight_);
		float scale = static_cast<float>(MazeHeight) / 2.0f + 2.0f; // 迷路の高さが画面に収まるようにスケールを調整
		projectionMatrix_ = DirectX::XMMatrixOrthographicOffCenterLH(-scale * aspectRatio, scale * aspectRatio, -scale, scale, 0.1f, 100.0f);

		CD3DX12_RANGE readRange(0, 0);
		ThrowIfFailed(constantBuffer_->Map(0, &readRange, reinterpret_cast<void**>(&pCbvDataBegin_)));
		memcpy(pCbvDataBegin_, &projectionMatrix_, sizeof(projectionMatrix_));
	}
}


void DXApplication::CreateD3D12Device(IDXGIFactory6* dxgiFactory, ID3D12Device** d3d12device)
{
	ID3D12Device* tmpDevice = nullptr;
	ComPtr<IDXGIAdapter1> hardwareAdapter;
	for (UINT adapterIndex = 0; DXGI_ERROR_NOT_FOUND != dxgiFactory->EnumAdapters1(adapterIndex, &hardwareAdapter); ++adapterIndex) {
		DXGI_ADAPTER_DESC1 desc;
		hardwareAdapter->GetDesc1(&desc);
		if (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) continue;
		if (SUCCEEDED(D3D12CreateDevice(hardwareAdapter.Get(), D3D_FEATURE_LEVEL_11_0, _uuidof(ID3D12Device), nullptr))) break;
	}
	D3D12CreateDevice(hardwareAdapter.Get(), D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(d3d12device));
}

void DXApplication::ThrowIfFailed(HRESULT hr)
{
	if (FAILED(hr))
	{
		char str[64] = {};
		sprintf_s(str, "HRESULT of 0x%08X", static_cast<UINT>(hr));
		throw std::runtime_error(std::string(str));
	}
}