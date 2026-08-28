using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

// Expose internal types (e.g. AttributeListRecord) to the test project so the
// $ATTRIBUTE_LIST NameOffset behaviour can be unit tested directly.
[assembly: InternalsVisibleTo("LibraryTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010047ebec172a9831bb20fede77e17d784026ea7030d7055f2ae09576c71cebe77ebfab436d80580a4fcbba7242ff61bd52b686f5fe9d41fe7cd3e6c05b8a876eccf35b8ad7c5e3a6704295d7210b138d7280a6f72688419a65dd7a8612d66869f2e712c57c41fcc9196e4cb06d95d8e678f6967e65348c370405fb7eeb6aa1d3e8")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.

[assembly: ComVisible(false)]

#if NET5_0_OR_GREATER
[assembly: SupportedOSPlatform("windows")]
#endif

// The following GUID is for the ID of the typelib if this project is exposed to COM

[assembly: Guid("9095333e-08e4-4c13-b556-20b5fdb9db2a")]