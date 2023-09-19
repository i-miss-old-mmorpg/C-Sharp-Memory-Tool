using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

class Program
{
    const int PROCESS_ALL_ACCESS = 0x1F0FFF;
    const int MEM_COMMIT = 0x1000;
    const int PAGE_READWRITE = 0x04;

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(int hProcess, long lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, int[] lpBuffer, int nSize, IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    public static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    static void Main(string[] args)
    {
        Console.Write("Enter the process name: ");
        string processName = Console.ReadLine();

        Console.Write("Enter the integer to search: ");
        int searchInt;

        while (true)
        {
            Console.Write("Please enter an integer: ");
            string input = Console.ReadLine();

            if (int.TryParse(input, out searchInt))
            {
                break; // break the loop if input is valid
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter an integer.");
            }
        }

        Process process = Process.GetProcessesByName(processName)[0];
        IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);

        MEMORY_BASIC_INFORMATION mbi = new MEMORY_BASIC_INFORMATION();

        long address = 0;
        List<long> addresses = new List<long>();

        // Initial search
        RunSearch(processHandle, searchInt, ref address, ref mbi, addresses);

        // Loop for further refining search
        while (true)
        {
            Console.WriteLine("Enter the next integer to search: ");
            Console.WriteLine("(Alternatively, type 'exit' to terminate the program or 'write' to modify an address)");

            string input = Console.ReadLine();

            if (input.ToLower() == "exit") break;

            if (input.ToLower() == "write")
            {
                long addressToEdit = GetAddressToEdit();
                int valueToInput = GetValueToInput();

                WriteMemory(processHandle, addressToEdit, valueToInput);
            }
            else
            {
                searchInt = GetInteger(input);

                // Refine search
                addresses = RefineSearch(processHandle, searchInt, addresses);
            }
        }
    }

    static void RunSearch(IntPtr processHandle, int searchInt, ref long address, ref MEMORY_BASIC_INFORMATION mbi, List<long> addresses)
    {
        int resultsCount = 0;
        while (VirtualQueryEx(processHandle, (IntPtr)address, out mbi, (uint)Marshal.SizeOf(mbi)) != 0)
        {
            if (mbi.State == MEM_COMMIT && mbi.Protect == PAGE_READWRITE)
            {
                int bytesRead = 0;
                byte[] buffer = new byte[(int)mbi.RegionSize];

                if (ReadProcessMemory((int)processHandle, (long)mbi.BaseAddress, buffer, buffer.Length, ref bytesRead))
                {
                    for (int i = 0; i < buffer.Length; i += 4)
                    {
                        if (i + 3 < buffer.Length)
                        {
                            Int32 value = BitConverter.ToInt32(buffer, i);

                            if (value == searchInt)
                            {
                                long foundAddress = address + i;
                                addresses.Add(foundAddress);

                                if (resultsCount < 100)
                                {
                                    Console.WriteLine($"Found {searchInt} at address: 0x{foundAddress:X}");
                                }
                                else if (resultsCount == 100)
                                {
                                    Console.WriteLine("* Subsequent addresses are omitted. *");
                                }
                                resultsCount++;
                            }
                        }
                    }
                }
            }

            address = (long)mbi.BaseAddress + (long)mbi.RegionSize;
        }

        Console.WriteLine($"Total results found: {resultsCount}");
    }

    static List<long> RefineSearch(IntPtr processHandle, int searchInt, List<long> addresses)
    {
        int resultsCount = 0;
        List<long> refinedAddresses = new List<long>();
        foreach (long address in addresses)
        {
            byte[] buffer = new byte[4];
            int bytesRead = 0;

            if (ReadProcessMemory((int)processHandle, address, buffer, buffer.Length, ref bytesRead))
            {
                Int32 value = BitConverter.ToInt32(buffer, 0);
                if (value == searchInt)
                {
                    refinedAddresses.Add(address);

                    if (resultsCount < 100)
                    {
                        Console.WriteLine($"Found {searchInt} at address: 0x{address:X}");
                    }
                    else if (resultsCount == 100)
                    {
                        Console.WriteLine("* Subsequent addresses are omitted. *");
                    }
                    resultsCount++;
                }
            }
        }

        Console.WriteLine($"Refined results count: {refinedAddresses.Count}");

        return refinedAddresses;
    }


    static void WriteMemory(IntPtr processHandle, long baseAddress, int value)
    {
        try
        {
            WriteProcessMemory(processHandle, (IntPtr)baseAddress, new int[] { value }, 4, IntPtr.Zero);
            Console.WriteLine("Memory successfully written.");
        }
        catch
        {
            Console.WriteLine("Error writing memory.");
        }
    }

    // Method to get the address to edit from the user
    static long GetAddressToEdit()
    {
        Console.Write("Enter the address to edit: ");
        string input;
        long address;
        while (true)
        {
            input = Console.ReadLine();
            if (long.TryParse(input.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address))
            {
                break;
            }
            Console.WriteLine("Invalid input. Please enter a hexadecimal address beginning with '0x': ");
        }
        return address;
    }

    // Method to get the value to input from the user
    static int GetValueToInput()
    {
        Console.Write("Enter the value to input: ");
        string input;
        int value;
        while (true)
        {
            input = Console.ReadLine();
            if (int.TryParse(input, out value))
            {
                break;
            }
            Console.WriteLine("Invalid input. Please enter an integer: ");
        }
        return value;
    }

    // Method to get the integer to search from the user
    static int GetInteger(string input)
    {
        int value;
        while (true)
        {
            if (int.TryParse(input, out value))
            {
                break;
            }
            Console.Write("Invalid input. Please enter an integer: ");
        }
        return value;
    }
}