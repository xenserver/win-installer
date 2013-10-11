
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



latest_tar_source_files = {
        "xenbus" : "http://xenbus-build.uk.xensource.com:8080/job/XENBUS-upstream.git/lastSuccessfulBuild/artifact/xenbus.tar",
        "xenvif" : "http://xenvif-build.uk.xensource.com:8080/job/XENVIF-upstream.git/lastSuccessfulBuild/artifact/xenvif.tar",
        "xennet" : "http://xennet-build.uk.xensource.com:8080/job/XENNET-upstream.git/lastSuccessfulBuild/artifact/xennet.tar",
        "xeniface" : "http://xeniface-build.uk.xensource.com:8080/job/XENIFACE-upstream.git/lastSuccessfulBuild/artifact/xeniface.tar",
        "xenvbd" : "http://xenvbd-build.uk.xensource.com:8080/job/XENVBD-upstream.git/lastSuccessfulBuild/artifact/xenvbd.tar",
        "xenguestagent" : "http://xeniface-build.uk.xensource.com:8080/job/GUEST%20AGENT-upstream.git/lastSuccessfulBuild/artifact/xenguestagent.tar",
        "xenvss" : "http://xenvbd-build.uk.xensource.com:8080/job/XENVSS.git/lastSuccessfulBuild/artifact/xenvss.tar",
        }

all_drivers_signed = False

