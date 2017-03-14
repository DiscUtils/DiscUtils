##### apple-test.zip
File to test a specific set of issues with ISO9660 parsing. https://github.com/DiscUtils/DiscUtils/pull/8

It appears that hdiutil, a utility for creating iso-9660 images, was the source if the non-complient image that prompted the above pull request. The test file was created on a Linux system, and then modified by hand to demonstrate the issues in the original.

The problem can be detected on Linux using two different tools:

isoinfo:
**BAD RRVERSION (8)
(Seems to check version before signature and length).

isovfy:
RRlen=100 [AA,PX,TF,**BAD SUSP 0 80]
(Seems to check signature first, similar to the fix in the above pull request).

This issue is discussed in a couple online threads:

https://bugs.launchpad.net/ubuntu/+source/cdrkit/+bug/57796
https://bugs.debian.org/cgi-bin/bugreport.cgi?bug=457308

There are a couple of Linux kernel code commits related to this issue, as well (between 2005 and 2008):

https://github.com/torvalds/linux/commits/master/fs/isofs/rock.c
