#include "Win32Application.h"
#include "DXApplication.h" // この行を忘れていないか？

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow)
{
	// この2行がチュートリアルの最終形
	DXApplication dxApp(1280, 720, L"DX MAZE GAME");
	Win32Application::Run(&dxApp, hInstance);

	return 0;
}