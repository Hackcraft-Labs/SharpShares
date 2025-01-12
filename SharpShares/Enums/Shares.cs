﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace SharpShares.Enums
{
    class Shares
    {
        [DllImport("Netapi32.dll", SetLastError = true)]
        public static extern int NetWkstaGetInfo(string servername, int level, out IntPtr bufptr);

        [DllImport("Netapi32.dll", SetLastError = true)]
        static extern int NetApiBufferFree(IntPtr Buffer);

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetShareEnum(
            StringBuilder ServerName,
            int level,
            ref IntPtr bufPtr,
            uint prefmaxlen,
            ref int entriesread,
            ref int totalentries,
            ref int resume_handle
        );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WKSTA_INFO_100
        {
            public int platform_id;
            public string computer_name;
            public string lan_group;
            public int ver_major;
            public int ver_minor;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHARE_INFO_0
        {
            public string shi0_netname;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHARE_INFO_1
        {
            public string shi1_netname;
            public uint shi1_type;
            public string shi1_remark;
            public SHARE_INFO_1(string sharename, uint sharetype, string remark)
            {
                this.shi1_netname = sharename;
                this.shi1_type = sharetype;
                this.shi1_remark = remark;
            }
            public override string ToString()
            {
                return shi1_netname;
            }
        }

        const uint MAX_PREFERRED_LENGTH = 0xFFFFFFFF;
        const int NERR_Success = 0;

        private enum NetError : uint
        {
            NERR_Success = 0,
            NERR_BASE = 2100,
            NERR_UnknownDevDir = (NERR_BASE + 16),
            NERR_DuplicateShare = (NERR_BASE + 18),
            NERR_BufTooSmall = (NERR_BASE + 23),
        }

        private enum SHARE_TYPE : uint
        {
            STYPE_DISKTREE = 0,
            STYPE_PRINTQ = 1,
            STYPE_DEVICE = 2,
            STYPE_IPC = 3,
            STYPE_SPECIAL = 0x80000000,
        }

        public static SHARE_INFO_1[] EnumNetShares(string Server)
        {
            List<SHARE_INFO_1> ShareInfos = new List<SHARE_INFO_1>();
            int entriesread = 0;
            int totalentries = 0;
            int resume_handle = 0;
            int nStructSize = Marshal.SizeOf(typeof(SHARE_INFO_1));
            IntPtr bufPtr = IntPtr.Zero;
            StringBuilder server = new StringBuilder(Server);
            int ret = NetShareEnum(server, 1, ref bufPtr, MAX_PREFERRED_LENGTH, ref entriesread, ref totalentries, ref resume_handle);
            if (ret == NERR_Success)
            {
                IntPtr currentPtr = bufPtr;
                for (int i = 0; i < entriesread; i++)
                {
                    SHARE_INFO_1 shi1 = (SHARE_INFO_1)Marshal.PtrToStructure(currentPtr, typeof(SHARE_INFO_1));
                    ShareInfos.Add(shi1);
                    currentPtr += nStructSize;
                }
                NetApiBufferFree(bufPtr);
                return ShareInfos.ToArray();
            }
            else
            {
                ShareInfos.Add(new SHARE_INFO_1("ERROR=" + ret.ToString(), 10, string.Empty));
                return ShareInfos.ToArray();
            }
        }

        public static bool Juicy(string token, Utilities.Options.Arguments arguments)
        {
            foreach (string s in arguments.juicy)
            {
                if(token.ToLower().Contains(s))
                    return true;
            }

            return false;
        }

        public static void Spider(string computer, string share, string appendPath, Utilities.Options.Arguments arguments)
        {
            string path;

            if (appendPath == "")
                path = String.Format("\\\\{0}\\{1}", computer, share);
            else
                path = appendPath;

            try
            {
                var files = Directory.GetFiles(path);

                foreach (var file in files)
                {
                    if (Juicy(file, arguments))
                        Console.WriteLine(String.Format("[+] Juicy file: {0}", file));
                    else
                        Console.WriteLine(file);
                }
            }
            catch (UnauthorizedAccessException e) {}
            catch (Exception e) { Console.WriteLine(e); }

            try
            {
                var directories = Directory.GetDirectories(path);

                foreach (var dir in directories)
                {
                    if (Juicy(dir, arguments))
                        Console.WriteLine(String.Format("[+] Juicy directory: {0}", dir));

                    Spider(computer, share, dir, arguments);
                    NapTime(arguments);
                }
            }
            catch (UnauthorizedAccessException e) {}
            catch (Exception e) { Console.WriteLine(e); }
        }

        public static void GetComputerShares(string computer, Utilities.Options.Arguments arguments)
        {
            //Error 53 - network path was not found
            //Error 5 - Access Denied
            string[] errors = { "ERROR=53", "ERROR=5" };
            SHARE_INFO_1[] computerShares = EnumNetShares(computer);
            if (computerShares.Length > 0)
            {
                List<string> readableShares = new List<string>();
                List<string> writeableShares = new List<string>();
                List<string> unauthorizedShares = new List<string>();
                // get current user's identity to compare against ACL of shares
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                string userSID = identity.User.Value;
                foreach (SHARE_INFO_1 share in computerShares)// <------------ go to next share -----------+
                {                                                                                       // |
                    if ((arguments.filter != null) && (arguments.filter.Contains(share.shi1_netname.ToString().ToUpper())))  // |
                    {                                                                                   // |
                        continue; // Skip the remainder of this iteration. --------------------------------+
                    }
                    //share.shi1_netname returns the error code when caught
                    if (arguments.stealth && !errors.Contains(share.shi1_netname))
                    {
                        Console.WriteLine("[?] \\\\{0}\\{1}", computer, share.shi1_netname);
                        continue; //do not perform access checks
                    }
                    try
                    {
                        string path = String.Format("\\\\{0}\\{1}", computer, share.shi1_netname);
                        var files = Directory.GetFiles(path);
                        
                        readableShares.Add(share.shi1_netname);
                        AuthorizationRuleCollection rules = Directory.GetAccessControl(path).GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                        foreach (FileSystemAccessRule rule in rules)
                        {
                            //https://stackoverflow.com/questions/130617/how-do-you-check-for-permissions-to-write-to-a-directory-or-file
                            // compare SID of group referenced in ACL to groups the current user is a member of
                            if (rule.IdentityReference.ToString() == userSID || identity.Groups.Contains(rule.IdentityReference))
                            {
                                // plenty of other FileSystem Rights to look for
                                // https://docs.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.filesystemrights
                                if ((//rule.FileSystemRights.HasFlag(FileSystemRights.CreateFiles) ||
                                     //rule.FileSystemRights.HasFlag(FileSystemRights.WriteAttributes) ||
                                     //rule.FileSystemRights.HasFlag(FileSystemRights.WriteData) ||
                                     //rule.FileSystemRights.HasFlag(FileSystemRights.WriteExtendedAttributes) ||
                                     //rule.FileSystemRights.HasFlag(FileSystemRights.CreateDirectories) ||
                                    rule.FileSystemRights.HasFlag(FileSystemRights.Write)) && rule.AccessControlType == AccessControlType.Allow)
                                {
                                    writeableShares.Add(share.shi1_netname);
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        //share.shi1_netname returns the error code when caught
                        if (!errors.Contains(share.shi1_netname))
                        {
                            unauthorizedShares.Add(share.shi1_netname);
                        }
                    }
                }
                if (readableShares.Count > 0)
                {
                    foreach (string share in readableShares)
                    {
                        string output = String.Format("[r] \\\\{0}\\{1}", computer, share);
                        if (!String.IsNullOrEmpty(arguments.outfile))
                        {
                            try
                            {
                                WriteToFileThreadSafe(output, arguments.outfile);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[!] Outfile Error: {0}", ex.Message);
                                //Environment.Exit(0);
                            }
                        }
                        else
                        {
                            Console.WriteLine(output);
                        }

                        if (arguments.spider)
                        {
                            Spider(computer, share, "", arguments);
                            NapTime(arguments);
                        }
                    }
                }
                if (writeableShares.Count > 0)
                {
                    foreach (string share in writeableShares)
                    {
                        string output = String.Format("[w] \\\\{0}\\{1}", computer, share);
                        if (!String.IsNullOrEmpty(arguments.outfile))
                        {
                            try
                            {
                                WriteToFileThreadSafe(output, arguments.outfile);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[!] Outfile Error: {0}", ex.Message);
                                //Environment.Exit(0);
                            }
                        }
                        else
                        {
                            Console.WriteLine(output);
                        }
                    }
                }
                if (arguments.verbose && unauthorizedShares.Count > 0)
                {
                    foreach (string share in unauthorizedShares)
                    {
                        string output = String.Format("[-] \\\\{0}\\{1}", computer, share);
                        if (!String.IsNullOrEmpty(arguments.outfile))
                        {
                            try
                            {
                                WriteToFileThreadSafe(output, arguments.outfile);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[!] Outfile Error: {0}", ex.Message);
                                //Environment.Exit(0);
                            }
                        }
                        else
                        {
                            Console.WriteLine(output);
                        }
                    }
                }
            }

            Utilities.Status.currentCount += 1;


        }
        
        public static ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim();

        public static void WriteToFileThreadSafe(string text, string path)
        {
            // Set Status to Locked
            _readWriteLock.EnterWriteLock();
            try
            {
                // Append text to the file
                using (StreamWriter sw = File.AppendText(path))
                {
                    sw.WriteLine(text);
                    sw.Close();
                }
            }
            finally
            {
                // Release lock
                _readWriteLock.ExitWriteLock();
            }
        }

        public static void NapTime(Utilities.Options.Arguments arguments)
        {
            int baseSleepTime = arguments.sleep * 1000;
            double jitterPercentage = arguments.jitter / 100;

            int minSleepTime = (int)(baseSleepTime - (baseSleepTime * jitterPercentage));
            int maxSleepTime = (int)(baseSleepTime + (baseSleepTime * jitterPercentage));

            Random random = new Random();
            Thread.Sleep(random.Next(minSleepTime, maxSleepTime));
        }

        public static void GetAllShares(List<string> computers, Utilities.Options.Arguments arguments)
        {
            Console.WriteLine("[+] Starting share enumeration against {0} hosts\n", computers.Count);

            foreach (string computer in computers)
            {
                GetComputerShares(computer, arguments);
                NapTime(arguments);
            }
            
            Console.WriteLine("[+] Finished Enumerating Shares");
        }

    }
}
