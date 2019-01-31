using System;
using System.Text;
using DiscUtils.Xfs;
using Xunit;

namespace LibraryTests.Xfs
{
    public class XfsTests
    {
        [Fact]
        public void CanReadSymlink()
        {
            var context = new Context
            {
                SuperBlock = new SuperBlock(),
                Options = new XfsFileSystemOptions { FileNameEncoding = Encoding.UTF8}
            };

            var inode = new Inode(1, context);
            inode.ReadFrom(GetInodeBuffer(), 0);

            var symlink = new Symlink(context, inode);
            Assert.Equal("init.d", symlink.TargetPath);


            inode = new Inode(1, context);
            var inodeBuffer = GetInodeBuffer();
            inodeBuffer[0x6C] = 60; //garbage after first null byte
            inode.ReadFrom(inodeBuffer, 0);

            symlink = new Symlink(context, inode);
            Assert.Equal("init.d", symlink.TargetPath);
        }

        private byte[] GetInodeBuffer()
        {
            var inodeBuffer = new byte[0x70];
            inodeBuffer[0x5] = (byte)InodeFormat.Local;
            inodeBuffer[0x3F] = 6;//Length (di_size)
            inodeBuffer[0x52] = 0;//Forkoff
            inodeBuffer[0X64] = (byte)'i';
            inodeBuffer[0X65] = (byte)'n';
            inodeBuffer[0X66] = (byte)'i';
            inodeBuffer[0X67] = (byte)'t';
            inodeBuffer[0X68] = (byte)'.';
            inodeBuffer[0X69] = (byte)'d';

            return inodeBuffer;
        }
    }
}