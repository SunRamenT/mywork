// Win32Application.cpp (マウス入力対応版)

#include "Win32Application.h"
#include <windowsx.h> // GET_X_LPARAM, GET_Y_LPARAM を使うために追加

// DXApplicationのポインタをウィンドウに紐付ける
DXApplication* Win32Application::p_dxApp = nullptr;

void Win32Application::Run(DXApplication* dxApp, HINSTANCE hInstance)
{
	p_dxApp = dxApp; // ポインタを静的メンバーに保存

	WNDCLASSEX windowClass = {};
	windowClass.cbSize = sizeof(WNDCLASSEX);
	windowClass.style = CS_HREDRAW | CS_VREDRAW;
	windowClass.lpfnWndProc = WindowProc;
	windowClass.hInstance = hInstance;
	windowClass.hCursor = LoadCursor(NULL, IDC_ARROW);
	windowClass.lpszClassName = L"DXSampleClass";
	RegisterClassEx(&windowClass);

	RECT windowRect = { 0, 0, static_cast<LONG>(dxApp->GetWindowWidth()), static_cast<LONG>(dxApp->GetWindowHeight()) };
	AdjustWindowRect(&windowRect, WS_OVERLAPPEDWINDOW, false);

	HWND hwnd = CreateWindow(
		windowClass.lpszClassName,
		dxApp->GetTitle(),
		WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT,
		CW_USEDEFAULT,
		windowRect.right - windowRect.left,
		windowRect.bottom - windowRect.top,
		nullptr,
		nullptr,
		hInstance,
		dxApp // CreateWindowの最後の引数にdxAppのポインタを渡す
	);

	dxApp->OnInit(hwnd);
	ShowWindow(hwnd, SW_SHOW);

	MSG msg = {};
	while (msg.message != WM_QUIT)
	{
		if (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
		{
			TranslateMessage(&msg);
			DispatchMessage(&msg);
		}
		else
		{
			dxApp->OnUpdate();
			dxApp->OnRender();
		}
	}

	dxApp->OnDestroy();
	UnregisterClass(windowClass.lpszClassName, windowClass.hInstance);
}

LRESULT CALLBACK Win32Application::WindowProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam)
{
	// WM_CREATEメッセージでポインタを取得して紐付ける
	if (message == WM_CREATE)
	{
		CREATESTRUCT* pCreate = reinterpret_cast<CREATESTRUCT*>(lparam);
		SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pCreate->lpCreateParams));
		return 0;
	}

	// ポインタを取得
	DXApplication* dxApp = reinterpret_cast<DXApplication*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));

	switch (message)
	{
	case WM_DESTROY:
		PostQuitMessage(0);
		return 0;

		// マウスの左ボタンが押された時の処理を追加
	case WM_LBUTTONDOWN:
		if (dxApp)
		{
			dxApp->OnClick(); // DXApplicationのOnClickを呼び出す
		}
		return 0;
	}

	return DefWindowProc(hwnd, message, wparam, lparam);
}