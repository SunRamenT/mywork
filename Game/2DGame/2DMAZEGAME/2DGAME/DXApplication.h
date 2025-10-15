// DXApplication.h (プレイヤー対応版)

#pragma once
#include <windows.h>
#include <string>
#include <vector>
#include <stdexcept>
#include <wrl.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include <D3Dcompiler.h>
#include <DirectXMath.h>
#include "d3dx12.h"
#include "MazeGenerator.h" // Vector2Int を使うためにインクルード

#pragma comment(lib, "d3d12.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib,"d3dcompiler.lib")
#pragma comment(lib, "gdi32.lib") 
using Microsoft::WRL::ComPtr;

struct InstanceData
{
	DirectX::XMFLOAT3 position;
	DirectX::XMFLOAT4 color;
};

struct GameObject
{
	Vector2Int gridPos;
	bool isActive = true;
};

enum class GameScene
{
	Title,
	InGame,
	GameClear,
	GameOver
};

class DXApplication
{
public:
	static const int kFrameCount = 2;
	static const int MazeWidth = 25;  // 迷路の幅
	static const int MazeHeight = 19; // 迷路の高さ

	DXApplication(unsigned int width, unsigned int height, std::wstring title);
	~DXApplication();

	void OnInit(HWND hwnd);
	void OnUpdate();
	void OnRender();
	void OnDestroy();
	void OnClick(); // ★★★ OnClick関数を宣言 ★★★

	const WCHAR* GetTitle() const { return title_.c_str(); }
	unsigned int GetWindowWidth() const { return windowWidth_; }
	unsigned int GetWindowHeight() const { return windowHeight_; }

private:
	HWND hwnd_; // ★ウィンドウハンドルを保持
	HWND hudHwnd_; // ★ HUDウィンドウのハンドル
	std::wstring title_;
	unsigned int windowWidth_;
	unsigned int windowHeight_;

	CD3DX12_VIEWPORT viewport_;
	CD3DX12_RECT scissorrect_;

	// パイプラインオブジェクト
	ComPtr<ID3D12Device> device_;
	ComPtr<ID3D12CommandAllocator> commandAllocator_;
	ComPtr<ID3D12GraphicsCommandList> commandList_;
	ComPtr<ID3D12CommandQueue> commandQueue_;
	ComPtr<IDXGISwapChain4> swapchain_;
	ComPtr<ID3D12DescriptorHeap> rtvHeaps_;
	ComPtr<ID3D12Resource> renderTargets_[kFrameCount];
	ComPtr<ID3D12PipelineState> pipelinestate_;
	ComPtr<ID3D12RootSignature> rootsignature_;

	// リソース
	ComPtr<ID3D12Resource> vertexBuffer_;
	D3D12_VERTEX_BUFFER_VIEW vertexBufferView_;
	ComPtr<ID3D12Resource> indexBuffer_;
	D3D12_INDEX_BUFFER_VIEW indexBufferView_;
	ComPtr<ID3D12Resource> instanceBuffer_;
	D3D12_VERTEX_BUFFER_VIEW instanceBufferView_;
	UINT instanceCount_;
	ComPtr<ID3D12DescriptorHeap> cbvHeap_;
	ComPtr<ID3D12Resource> constantBuffer_;
	DirectX::XMMATRIX projectionMatrix_;
	UINT8* pCbvDataBegin_;

	// フェンス
	ComPtr<ID3D12Fence> fence_;
	UINT64 fenceValue_;
	HANDLE fenceEvent_;

	// ゲームロジック用メンバ変数
	std::vector<std::vector<int>> mazeData_;
	Vector2Int playerGridPos_;
	UINT8* pInstanceDataBegin_;
	float moveCooldown_;
	Vector2Int goalGridPos_;
	std::vector<GameObject> keys_;
	bool goalActive_;

	// ★★★ シーン管理用メンバ変数を追加 ★★★
	GameScene currentScene_;
	float timeLimit_;

	// メンバ関数
	void LoadPipeline(HWND hwnd);
	void LoadAssets();
	void WaitForGpu();
	void MovePlayer(Vector2Int direction);
	void CreateD3D12Device(IDXGIFactory6* dxgiFactory, ID3D12Device** d3d12device);
	void ThrowIfFailed(HRESULT hr);

	// シーンごとの処理関数
	void ResetGame();
	void UpdateTitle();
	void UpdateInGame();
	void UpdateResult();
	void RenderTitle();
	void RenderInGame();
	void RenderResult();
	void RenderInGameUI();
};