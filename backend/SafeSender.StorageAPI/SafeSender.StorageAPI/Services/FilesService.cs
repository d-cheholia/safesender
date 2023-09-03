using Microsoft.Extensions.Options;
using SafeSender.StorageAPI.Interfaces;
using SafeSender.StorageAPI.Models;
using SafeSender.StorageAPI.Models.ApiModels;
using SafeSender.StorageAPI.Options;

namespace SafeSender.StorageAPI.Services;

/// <summary>
/// Files service
/// </summary>
public class FilesService : IFilesService
{
    private readonly IFilesInternalInfosRepository _filesInternalInfosRepository;
    private readonly IFilesRepository _filesRepository;
    private readonly IOptionsMonitor<StorageOptions> _storageOptions;
    private readonly ILogger<FilesService> _logger;

    /// <summary>
    /// Constructor for <see cref="FilesService"/>
    /// </summary>
    /// <param name="filesInternalInfosRepository">Files internal information repository</param>
    /// <param name="filesRepository">Files repository</param>
    /// <param name="storageOptions">Storage options</param>
    /// <param name="logger">Logger</param>
    public FilesService(IFilesInternalInfosRepository filesInternalInfosRepository,
        IFilesRepository filesRepository,
        IOptionsMonitor<StorageOptions> storageOptions, 
        ILogger<FilesService> logger)
    {
        _filesInternalInfosRepository = filesInternalInfosRepository;
        _filesRepository = filesRepository;
        _storageOptions = storageOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> UploadFile(UploadFileRequestModel model)
    {
        var fileInternalInfo = new FileInternalInfo
        {
            FileName = model.FileName,
            PasswordHash = model.PasswordHash,
            StorageType = _storageOptions.CurrentValue.Type,
            OriginalFileSize = model.OriginalFileSize,
        };

        var fileExtension = Path.GetExtension(model.FileName);
        
        var fileSaveInfo = await _filesRepository.SaveFileBytes(fileInternalInfo.Token + fileExtension, model.FileData);

        if (!fileSaveInfo.Status)
        {
            // TODO: Add exception type
            throw new Exception("UploadFile - File saving failed.");
        }
        
        fileInternalInfo.StorageFileIdentifier = fileSaveInfo.StorageFileIdentifier;
        
        await _filesInternalInfosRepository.Add(fileInternalInfo);

        _logger.LogInformation("UploadFile - File info added. External token: {ExternalToken}, Internal token: {InternalToken}", 
            fileInternalInfo.StorageFileIdentifier, fileInternalInfo.Token);
        
        return fileInternalInfo.Token;
    }

    /// <inheritdoc />
    public async Task<DownloadFileResponseModel> DownloadFile(string token)
    {
        var fileInternalInfo = await _filesInternalInfosRepository.GetByToken(token);
        
        if (fileInternalInfo is null)
        {
            throw new FileNotFoundException(
                $"DownloadFile - Information about file by specified token not found. Token: {token}");
        }

        var fileData = await _filesRepository.GetFileBytes(fileInternalInfo.StorageFileIdentifier);
        
        return new()
        {
            FileData = fileData,
            FileName = fileInternalInfo.FileName,
            PasswordHash = fileInternalInfo.PasswordHash,
            OriginalFileSize = fileInternalInfo.OriginalFileSize,
        };
    }
}