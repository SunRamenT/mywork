// Win32Application.h (変更はほとんどありません)

#pragma once
#include <windows.h>
#include "DXApplication.h"

class Win32Application
{
public:
	static void Run(DXApplication* dxApp, HINSTANCE hInstance);

private:
	// 静的メンバーとしてポインタを保持（より安全な方法）
	static DXApplication* p_dxApp;
	static LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam);
};