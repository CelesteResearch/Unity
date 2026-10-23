#pragma once
#include "fmod.h"
#include <cstdint>

extern "C"
{
    int CelesteBankIO_Start();
    uintptr_t CelesteBankIO_GetExchange();
    int CelesteBankIO_SetFile(unsigned int bank, unsigned int size);
    int CelesteBankIO_Install(unsigned int token, FMOD_SYSTEM* core);
    int CelesteBankIO_ReleaseOwner(unsigned int token);
    int CelesteBankIO_Stop();
}

extern "C" int CelesteBankIO_Fail();
