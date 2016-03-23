#include "stdafx.h"

UINT __stdcall SetInitiatorSidProperty(MSIHANDLE hInstall)
{
    HANDLE      processHandle = GetCurrentProcess();
    HANDLE      tokenHandle   = NULL;
    DWORD       tokenInfLen   = 0;
    PTOKEN_USER pTokenUser    = NULL;
    LPTSTR      strSid        = NULL;
    DWORD       error         = ERROR_SUCCESS;
    HRESULT     hr            = S_OK;

    hr = WcaInitialize(hInstall, "SetInitiatorSidProperty");
    ExitOnFailure(hr, "Failed to initialize");

    WcaLog(LOGMSG_STANDARD, "Initialized.");

    if (!OpenProcessToken(processHandle, TOKEN_QUERY, &tokenHandle)) {
        hr = HRESULT_FROM_WIN32(GetLastError());
        ExitOnFailure(hr, "OpenProcessToken() failed");
    }

    /* Get buffer length */
    GetTokenInformation(
        tokenHandle,
        TokenUser,
        NULL,
        0,
        &tokenInfLen
    );

    if ((error = GetLastError()) != ERROR_INSUFFICIENT_BUFFER) {
        hr = HRESULT_FROM_WIN32(error);
        ExitOnFailure(hr, "GetTokenInformation() failed");
    }

    if ((pTokenUser = (PTOKEN_USER)malloc(tokenInfLen)) == NULL) {
        hr = HRESULT_FROM_WIN32(ERROR_NOT_ENOUGH_MEMORY);
        ExitOnFailure(hr, "malloc() failed");
    }

    if (!GetTokenInformation(
            tokenHandle,
            TokenUser,
            (LPVOID)pTokenUser,
            tokenInfLen,
            &tokenInfLen)) {
        hr = HRESULT_FROM_WIN32(GetLastError());
        ExitOnFailure(hr, "GetTokenInformation() failed");
    }

    if (!ConvertSidToStringSid(pTokenUser->User.Sid, &strSid)) {
        hr = HRESULT_FROM_WIN32(GetLastError());
        ExitOnFailure(hr, "ConvertSidToStringSid() failed");
    }

    hr = WcaSetProperty(_T("INITIATORSID"), strSid);
    ExitOnFailure(hr, "WcaSetProperty() failed");

LExit:
    if (strSid) {
        LocalFree(strSid);
    }

    if (pTokenUser) {
        free(pTokenUser);
        pTokenUser = NULL;
    }

    if (tokenHandle) {
        CloseHandle(tokenHandle);
    }

    error = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(error);
}
