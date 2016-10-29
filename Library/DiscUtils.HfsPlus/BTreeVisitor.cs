namespace DiscUtils.HfsPlus
{
    internal delegate int BTreeVisitor<Key>(Key key, byte[] data)
        where Key : BTreeKey;
}