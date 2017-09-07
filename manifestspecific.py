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

artifactory='https://repo.citrite.net:443/xs-local-build/'

build_tar_source_files = {
       "xenguestagent" : r'win-xenguestagent/REQ-330/win-xenguestagent-254/xenguestagent.tar',
       "xenbus" : r'win-xenbus/patchq-9.x-vs2012/win-xenbus-115/xenbus.tar',
       "xencons" : r'win-xencons/master/win-xencons-10/xencons.tar',
       "xenvif" : r'win-xenvif/patchq/win-xenvif-152/xenvif.tar',
       "xennet" : r'win-xennet/patchq/win-xennet-64/xennet.signed.tar',
       "xeniface" : r'win-xeniface/8.2/win-xeniface-102/xeniface.tar',
       "xenvbd" : r'win-xenvbd/patchq/win-xenvbd-158/xenvbd.tar',
       "xenvss" : r'win-xenvss/master/win-xenvss-18/xenvss.tar',
}

signed_drivers = { 
       "xenbus" : r'win-xenbus/patchq/win-xenbus-85/xenbus.signed.tar',
       "xenvif" : r'win-xenvif/patchq/win-xenvif-152/xenvif.signed.tar',
       "xennet" : r'win-xennet/patchq/win-xennet-64/xennet.signed.tar',
       "xeniface" : r'win-xeniface/patchq/win-xeniface-102/xeniface.signed.tar',
       "xenvbd" : r'win-xenvbd/patchq/win-xenvbd-158/xenvbd.signed.tar',
}
