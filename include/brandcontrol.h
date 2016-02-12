/* Copyright (c) Citrix Systems Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met:
 *
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer.
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

#pragma once
#include <stdint.h>
typedef struct {
    const uint8_t lang;
    const uint8_t sublang;
    const TCHAR ** list;
} dict;

#include "setupbranding.h"

const TCHAR *getBrandingString(int brandindex)
{
    static bool brandinit=0;
    static const dict *uidict = loc_def;
    if (!brandinit) {
        int i;
        LANGID id = GetUserDefaultUILanguage();
        for (i=0; i<=(sizeof(dicts)/sizeof(dict)); i++) {
            uint8_t sublang = (id&0xFF00)>>8;
            uint8_t lang = (id&0xFF);
            if (lang == dicts[i]->lang) {
                if (uidict->lang != lang){
                    uidict = dicts[i];
                    continue;
                }
                if (sublang == dicts[i]->sublang) {
                    uidict = dicts[i];
                    break;
                }
            }
        }
        brandinit = 1;
    }
    return uidict->list[brandindex];
}
