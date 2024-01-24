/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

#define WIN32_LEAN_AND_MEAN

#include <Windows.h>
#include "wil/resource.h"
#include <sstream>

#pragma comment(linker,"\"/manifestdependency:type='win32' \
name='Microsoft.Windows.Common-Controls' version='6.0.0.0' \
processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

namespace
{
	enum Status : int
	{
		STATUS_NO_ERROR = 0,
		STATUS_DIB_LOAD_FAILED,
		STATUS_OUT_OF_MEMORY,
		STATUS_RIOT_DLL_MISSING,
		STATUS_RIOT_ENTRY_POINT_NOT_FOUND,
		STATUS_RIOT_LOADFROMDIB_FAILED
	};

	int GetStatusForWin32Error(DWORD error)
	{
		switch (error)
		{
		case ERROR_NOT_ENOUGH_MEMORY:
		case ERROR_OUTOFMEMORY:
			return STATUS_OUT_OF_MEMORY;
		default:
			return STATUS_DIB_LOAD_FAILED;
		}
	}

	bool IsValidCommandLine(LPCWSTR commandLine)
	{
		// We expect the command line to contain a single argument.
		bool valid = commandLine && *commandLine != L'\0';

		if (valid)
		{
			LPCWSTR pCommandLine = commandLine;

			do
			{
				const wchar_t value = *pCommandLine;
				if (value == L' ' || value == L'\t')
				{
					// The command line contains multiple arguments.
					valid = false;
					break;
				}

				pCommandLine++;
			} while (*pCommandLine != L'\0');
		}

		return valid;
	}

	typedef bool(__cdecl RIOT_LoadFromDIB_U)(HANDLE hDIB, HWND hwndParent, const wchar_t* fileName, int flags);

#ifndef NDEBUG
	// Adapted from https://stackoverflow.com/a/20387632
	bool LaunchDebugger()
	{
		// Get System directory, typically c:\windows\system32
		std::wstring systemDir(MAX_PATH + 1, '\0');
		UINT nChars = GetSystemDirectoryW(&systemDir[0], static_cast<UINT>(systemDir.length()));
		if (nChars == 0) return false; // failed to get system directory
		systemDir.resize(nChars);

		// Get process ID and create the command line
		DWORD pid = GetCurrentProcessId();
		std::wostringstream s;
		s << systemDir << L"\\vsjitdebugger.exe -p " << pid;
		std::wstring cmdLine = s.str();

		// Start debugger process
		STARTUPINFOW si;
		ZeroMemory(&si, sizeof(si));
		si.cb = sizeof(si);

		PROCESS_INFORMATION pi;
		ZeroMemory(&pi, sizeof(pi));

		if (!CreateProcessW(NULL, &cmdLine[0], NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) return false;

		// Close debugger process handles to eliminate resource leak
		CloseHandle(pi.hThread);
		CloseHandle(pi.hProcess);

		// Wait for the debugger to attach
		while (!IsDebuggerPresent()) Sleep(100);

		// Stop execution so the debugger can take over
		DebugBreak();
		return true;
	}
#endif // !NDEBUG
}

int WINAPI wWinMain(
	_In_ HINSTANCE hInstance,
	_In_opt_ HINSTANCE hPrevInstance,
	_In_ LPWSTR lpCmdLine,
	_In_ int nShowCmd)
{
#ifndef NDEBUG
	LaunchDebugger();
#endif

	if (!IsValidCommandLine(lpCmdLine))
	{
		return 0;
	}

	const wchar_t* const fileMappingName = lpCmdLine;

	wil::unique_handle fileMappingHandle(OpenFileMappingW(FILE_MAP_READ, FALSE, fileMappingName));

	if (!fileMappingHandle)
	{
		return GetStatusForWin32Error(GetLastError());
	}

	wil::unique_mapview_ptr<void> fileMappingView(MapViewOfFile(fileMappingHandle.get(), FILE_MAP_READ, 0, 0, 0));

	if (!fileMappingView)
	{
		return GetStatusForWin32Error(GetLastError());
	}

	wil::unique_hmodule riotDll(LoadLibraryW(L"RIOT.dll"));

	if (!riotDll)
	{
		return STATUS_RIOT_DLL_MISSING;
	}

	RIOT_LoadFromDIB_U* pfnLoadFromDIB = reinterpret_cast<RIOT_LoadFromDIB_U*>(GetProcAddress(riotDll.get(), "RIOT_LoadFromDIB_U"));

	if (!pfnLoadFromDIB)
	{
		return STATUS_RIOT_ENTRY_POINT_NOT_FOUND;
	}

	bool result = pfnLoadFromDIB(fileMappingView.get(), 0, L"", 0);

	return result ? STATUS_NO_ERROR : STATUS_RIOT_LOADFROMDIB_FAILED;
}
