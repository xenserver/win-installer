
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

build_tar_source_files = {
        "xenbus" : "http://www.uk.xensource.com/distfiles/pvdrivers-win-signed/pvdrivers-win-clearwater-rtm-signed/xenbus-226-signed.tar",
        "xenvif" : "http://xenvif-build.uk.xensource.com:8080/job/XENVIF.git/4/artifact/xenvif.tar",
        "xennet" : "http://xennet-build.uk.xensource.com:8080/job/XENNET.git/4/artifact/xennet.tar",
        "xeniface" : "http://xeniface-build.uk.xensource.com:8080/job/Xeniface.git/3/artifact/xeniface.tar",
        "xenvbd" : "http://xenvbd-build.uk.xensource.com:8080/job/XENVBD.git/6/artifact/xenvbd.tar",
        "xenguestagent" : "http://xeniface-build.uk.xensource.com:8080/job/guest%20agent.git/29/artifact/xenguestagent.tar",
        "xenvss" : "http://xenvbd-build.uk.xensource.com:8080/job/XENVSS.git/6/artifact/xenvss.tar",
        } 

all_drivers_signed = False
