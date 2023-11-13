# SharpShares
Multithreaded C# .NET Assembly to enumerate and spider accessible network shares in a domain or a target list

Stealthy fork of [mitchmoser's SharpShares](https://github.com/mitchmoser/SharpShares) project

```
> .\SharpShares.exe help

Optional Arguments:
    /threads  - specify maximum number of parallel threads  (default=25)
    /dc       - specify domain controller to query (if not ran on a domain-joined host)
    /domain   - specify domain name (if not ran on a domain-joined host)
    /ldap     - query hosts from the following LDAP filters (default=all)
         :all - All enabled computers with 'primary' group 'Domain Computers'
         :dc  - All enabled Domain Controllers (not read-only DCs)
         :exclude-dc - All enabled computers that are not Domain Controllers or read-only DCs
         :servers - All enabled servers
         :servers-exclude-dc - All enabled servers excluding Domain Controllers or read-only DCs
    /ou       - specify LDAP OU to query enabled computer objects from
                ex: "OU=Special Servers,DC=example,DC=local"
    /stealth  - list share names without performing read/write access checks
    /filter   - list of comma-separated shares to exclude from enumeration
                default: SYSVOL,NETLOGON,IPC$,PRINT$
    /outfile  - specify file for shares to be appended to instead of printing to std out
    /verbose  - return unauthorized shares
    /spider   - print a list of all files existing within directories (and subdirectories) in identified shares
    /juicy    - list of comma-separated tokens to match in spidered files/folders to be reported as juicy
    /targets  - specify a comma-separated list of target hosts
    /sleep    - specify the time (in seconds) to sleep after each host is enumerated
    /jitter   - specify a jitter percentage for the sleeping pattern (0-100)
```

## New Features
- Sleep/Jitter support
- Share Spidering
- Identification of juicy files/folders/shares (list is configurable)
- Target specification to bypass LDAP enumeration

## Execute Assembly
```
execute-assembly /path/to/SharpShares.exe /ldap:all /filter:sysvol,netlogon,ipc$,print$
```
## Example Output
```
[+] Parsed Aguments:
        threads: 25
        ldap: all
        ou: none
        filter: SYSVOL,NETLOGON,IPC$,PRINT$
        stealth: False
        verbose: False
        outfile:

[*] Excluding SYSVOL,NETLOGON,IPC$,PRINT$ shares
[*] Starting share enumeration with thread limit of 25
[r] = Readable Share
[w] = Writeable Share
[-] = Unauthorized Share (requires /verbose flag)
[?] = Unchecked Share (requires /stealth flag)

[+] Performing LDAP query for all enabled computers with "primary" group "Domain Computers"...
[+] This may take some time depending on the size of the environment
[+] LDAP Search Results: 10
[+] Starting share enumeration against 10 hosts

[r] \\DC-01\CertEnroll
[r] \\DC-01\File History Backups
[r] \\DC-01\Folder Redirection
[r] \\DC-01\Shared Folders
[r] \\DC-01\Users
[w] \\WEB-01\wwwroot
[r] \\DESKTOP\ADMIN$
[r] \\DESKTOP\C$
[+] Finished Enumerating Shares
```
### Specifying Targets

The `/ldap` and `/ou` flags can be used together or seprately to generate a list of hosts to enumerate.

All hosts returned from these flags are combined and deduplicated before enumeration starts.

### Community

Join the Hackcraft community discord server [here](https://discord.gg/KZZfsnQsja). On the server you can receive support and discuss issues related to SharpShares.