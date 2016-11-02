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

artifactory='https://repo.citrite.net:443/xenserver/transformer/'

build_tar_source_files = {
       "xenguestagent" : r'win-xenguestagent/master/win-xenguestagent-212/xenguestagent.tar',
       "xenbus" : r'win-xenbus/patchq/win-xenbus-81/xenbus.tar',
       "xenvif" : r'win-xenvif/patchq/win-xenvif-91/xenvif.tar',
       "xennet" : r'win-xennet/patchq/win-xennet-58/xennet.tar',
       "xeniface" : r'win-xeniface/patchq/win-xeniface-50/xeniface.tar',
       "xenvbd" : r'win-xenvbd/patchq/win-xenvbd-146/xenvbd.tar',
       "xenvss" : r'win-xenvss/master/win-xenvss-18/xenvss.tar',
}

all_drivers_signed = False
