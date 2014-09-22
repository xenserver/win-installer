# Copyright (c) Citrix Systems Inc.
# All rights reserved.
#
# Redistribution and use in source and binary forms, 
# with or without modification, are permitted provided 
# that the following conditions are met:
#
# *   Redistributions of source code must retain the above 
#     copyright notice, this list of conditions and the 
#     following disclaimer.
# *   Redistributions in binary form must reproduce the above 
#     copyright notice, this list of conditions and the 
#     following disclaimer in the documentation and/or other 
#     materials provided with the distribution.
#
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
# CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
# INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
# MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
# DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
# CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
# SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
# BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
# SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
# INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
# WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
# NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
# OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
# SUCH DAMAGE.

secureserver = r'\\10.80.13.10\distfiles\distfiles\WindowsBuilds'
localserver = r'\\camos.uk.xensource.com\build\windowsbuilds\WindowsBuilds'

build_tar_source_files = {
       "xenbus" : r'standard-lcm\10\xenbus-7-2-0-47.tar',
       "xenvif" : r'xenvif.git.whql\52\xenvif-7-2-0-52.tar',
       "xennet" : r'standard-lcm\13\xennet-7-2-0-14.tar',
       "xeniface" : r'xeniface.git\17\xeniface.tar',
       "xenvbd" : r'standard-lcm\14\xenvbd-7-2-0-40.tar',
       "xenguestagent" : r'standard-lcm\11\xenguestagent-34.tar',
       "xenvss" : r'standard-lcm\16\xenvss-7.tar',
} 

all_drivers_signed = True
