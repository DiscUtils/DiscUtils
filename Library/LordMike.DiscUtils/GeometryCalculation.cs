namespace DiscUtils
{
    /// <summary>
    /// Delegate for calculating a disk geometry from a capacity.
    /// </summary>
    /// <param name="capacity">The disk capacity to convert.</param>
    /// <returns>The appropriate geometry for the disk.</returns>
    public delegate Geometry GeometryCalculation(long capacity);
}