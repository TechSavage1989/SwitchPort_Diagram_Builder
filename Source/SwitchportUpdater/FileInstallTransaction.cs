namespace SwitchportUpdater;

/// <summary>
/// Only handles a verified, local replacement. Process shutdown and visible startup
/// health checks belong to the native host. No settings or report files are touched.
/// </summary>
public sealed class FileInstallTransaction
{
    public string TargetPath {get;}
    public string BackupPath {get;}
    public string OriginalSha256 {get;}
    bool complete;
    FileInstallTransaction(string target,string backup,string originalSha256)
    {TargetPath=target;BackupPath=backup;OriginalSha256=originalSha256;}

    public static FileInstallTransaction Begin(string staged,string target,string backup,string expectedSha256,string? expectedOriginalSha256=null)
    {
        staged=Path.GetFullPath(staged);target=Path.GetFullPath(target);backup=Path.GetFullPath(backup);
        if(staged.Equals(target,StringComparison.OrdinalIgnoreCase)||staged.Equals(backup,StringComparison.OrdinalIgnoreCase)||target.Equals(backup,StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("The update source, target and rollback file must be separate files.");
        if(!File.Exists(target)||!File.Exists(staged))throw new UpdateException("The current or staged application is missing.");
        UpdateProtocol.Hash(expectedSha256);
        if(!ExecutableValidator.Sha256(staged).Equals(expectedSha256,StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("The staged application changed before installation.");
        RejectLink(staged);RejectLink(target);
        string original=ExecutableValidator.Sha256(target);
        if(expectedOriginalSha256!=null)
        {
            UpdateProtocol.Hash(expectedOriginalSha256);
            if(!original.Equals(expectedOriginalSha256,StringComparison.OrdinalIgnoreCase))
                throw new UpdateException("The current application changed after the update was confirmed. Check for updates again.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        if(File.Exists(backup))RejectLink(backup);
        AtomicCopy(target,backup,original);
        var transaction=new FileInstallTransaction(target,backup,original);
        // The same-install helper mutex is the primary concurrency guard. Recheck
        // the old file immediately before renaming as well, so a stale confirmation
        // cannot silently replace a file changed by another process.
        AtomicCopy(staged,target,expectedSha256,original);
        return transaction;
    }
    public void Commit(){complete=true;}
    public void Rollback()
    {
        if(complete)return;
        if(!File.Exists(BackupPath)||!ExecutableValidator.Sha256(BackupPath).Equals(OriginalSha256,StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("The previous executable could not be verified for rollback.");
        RejectLink(BackupPath);AtomicCopy(BackupPath,TargetPath,OriginalSha256);complete=true;
    }
    static void RejectLink(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new UpdateException("Update files cannot be symbolic links or reparse points.");
    }
    static void AtomicCopy(string source,string target,string hash,string? expectedTargetHash=null)
    {
        string temporary=target+".update-"+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.WriteThrough))
            {input.CopyTo(output);output.Flush(true);}
            if(!ExecutableValidator.Sha256(temporary).Equals(hash,StringComparison.OrdinalIgnoreCase))
                throw new UpdateException("A copied update file failed checksum verification.");
            if(expectedTargetHash!=null&&(!File.Exists(target)||!ExecutableValidator.Sha256(target).Equals(expectedTargetHash,StringComparison.OrdinalIgnoreCase)))
                throw new UpdateException("The installed application changed during update preparation. It was not replaced.");
            File.Move(temporary,target,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
