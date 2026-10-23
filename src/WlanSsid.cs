using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class WlanSsid
{
    private const uint WlanClientVersion = 2;
    private const int WlanIntfOpcodeCurrentConnection = 7;
    private const int Dot11SsidMaxLength = 32;

    public static string TryRead()
    {
        IntPtr client = IntPtr.Zero;
        IntPtr list = IntPtr.Zero;
        try
        {
            uint negotiated;
            if (WlanOpenHandle(WlanClientVersion, IntPtr.Zero, out negotiated, out client) != 0)
                return "";
            if (WlanEnumInterfaces(client, IntPtr.Zero, out list) != 0 || list == IntPtr.Zero)
                return "";

            WLAN_INTERFACE_INFO_LIST info = (WLAN_INTERFACE_INFO_LIST)Marshal.PtrToStructure(list, typeof(WLAN_INTERFACE_INFO_LIST));
            if (info.dwNumberOfItems < 1)
                return "";

            for (int i = 0; i < info.dwNumberOfItems; i++)
            {
                IntPtr itemPtr = new IntPtr(list.ToInt64() + 8 + i * Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO)));
                WLAN_INTERFACE_INFO item = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(itemPtr, typeof(WLAN_INTERFACE_INFO));
                if (item.isState != 1)
                    continue;

                IntPtr data;
                int dataSize;
                int opcodeType;
                if (WlanQueryInterface(client, ref item.InterfaceGuid, WlanIntfOpcodeCurrentConnection,
                        IntPtr.Zero, out dataSize, out data, out opcodeType) != 0 || data == IntPtr.Zero)
                    continue;
                try
                {
                    WLAN_CONNECTION_ATTRIBUTES conn = (WLAN_CONNECTION_ATTRIBUTES)Marshal.PtrToStructure(data, typeof(WLAN_CONNECTION_ATTRIBUTES));
                    string ssid = DecodeSsid(conn.wlanAssociationAttributes.dot11Ssid);
                    if (!string.IsNullOrEmpty(ssid))
                        return ssid;
                }
                finally
                {
                    WlanFreeMemory(data);
                }
            }
        }
        catch
        {
        }
        finally
        {
            if (list != IntPtr.Zero)
                WlanFreeMemory(list);
            if (client != IntPtr.Zero)
                WlanCloseHandle(client, IntPtr.Zero);
        }
        return "";
    }

    private static string DecodeSsid(DOT11_SSID ssid)
    {
        if (ssid.uSSIDLength == 0 || ssid.uSSIDLength > Dot11SsidMaxLength)
            return "";
        return Encoding.UTF8.GetString(ssid.ucSSID, 0, (int)ssid.uSSIDLength).Trim('\0');
    }

    [DllImport("wlanapi.dll")]
    private static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern int WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, int OpCode, IntPtr pReserved,
        out int pdwDataSize, out IntPtr ppData, out int pWlanOpcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_INTERFACE_INFO_LIST
    {
        public int dwNumberOfItems;
        public int dwIndex;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public int isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Dot11SsidMaxLength)]
        public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID dot11Ssid;
        public int dot11BssType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] dot11Bssid;
        public int dot11PhyType;
        public uint uDot11PhyIndex;
        public uint wlanSignalQuality;
        public uint ulRxRate;
        public uint ulTxRate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_CONNECTION_ATTRIBUTES
    {
        public int isState;
        public int wlanConnectionMode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
    }
}
