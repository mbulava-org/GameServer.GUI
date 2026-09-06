using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace GameServer.API.Client.Tests
{
    public class GeneratedApiClientsTests
    {
        private static HttpClient CreateMockHttpClient(Func<HttpResponseMessage> responseFactory)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseFactory);

            return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:5164") };
        }

        private static HttpClient CreateMockHttpClient(HttpResponseMessage response)
        {
            var statusCode = response.StatusCode;
            var contentBytes = response.Content != null ? response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult() : null;
            var mediaType = response.Content?.Headers.ContentType?.MediaType ?? "application/json";

            return CreateMockHttpClient(() =>
            {
                var msg = new HttpResponseMessage(statusCode);
                if (contentBytes != null)
                {
                    msg.Content = new ByteArrayContent(contentBytes);
                    msg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
                }
                return msg;
            });
        }

        private static HttpClient CreateMockJsonHttpClient<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            return CreateMockHttpClient(() => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        #region GameServersApi Tests

        [Fact]
        public async Task GameServersApi_GetAllAsync_DeserializesResponse()
        {
            var items = new[]
            {
                new GameServerListItemDto
                {
                    Id = 1,
                    ServerId = "srv-1",
                    Name = "Server 1",
                    ServiceName = "srv-1-svc",
                    Status = "Running",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsDeleted = false,
                    ResolvedPorts = new List<GameServerResolvedPortDto>(),
                    Ports = new List<GameServerPortDto>(),
                    Containers = new List<GameServerContainerDto>()
                }
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(items));
            var result = await api.GetAllAsync(false);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("srv-1", result.First().ServerId);
        }

        [Fact]
        public async Task GameServersApi_GetByServerIdAsync_DeserializesResponse()
        {
            var item = new GameServerDetailDto
            {
                Id = 1,
                ServerId = "srv-1",
                Name = "Server 1",
                ServiceName = "srv-1-svc",
                GameTypeRevisionId = 10,
                GameTypeKey = "valheim",
                Status = "Running",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false,
                Ports = new List<GameServerPortDto>(),
                Settings = new List<GameServerSettingDto>(),
                ResolvedPorts = new List<GameServerResolvedPortDto>(),
                ResolvedVolumes = new List<GameServerResolvedVolumeDto>(),
                ResolvedWebHosts = new List<GameServerResolvedWebHostDto>()
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(item));
            var result = await api.GetByServerIdAsync("srv-1");

            Assert.NotNull(result);
            Assert.Equal("srv-1", result.ServerId);
        }

        [Fact]
        public async Task GameServersApi_CreateAsync_DeserializesResponse()
        {
            var item = new GameServerDetailDto
            {
                Id = 1,
                ServerId = "srv-1",
                Name = "Server 1",
                ServiceName = "srv-1-svc",
                GameTypeRevisionId = 10,
                GameTypeKey = "valheim",
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false,
                Ports = new List<GameServerPortDto>(),
                Settings = new List<GameServerSettingDto>(),
                ResolvedPorts = new List<GameServerResolvedPortDto>(),
                ResolvedVolumes = new List<GameServerResolvedVolumeDto>(),
                ResolvedWebHosts = new List<GameServerResolvedWebHostDto>()
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(item, HttpStatusCode.Created));
            var result = await api.CreateAsync(new SaveGameServerRequestDto
            {
                Name = "Server 1",
                GameTypeRevisionId = 10
            });

            Assert.NotNull(result);
            Assert.Equal("srv-1", result.ServerId);
        }

        [Fact]
        public async Task GameServersApi_UpdateAsync_DeserializesResponse()
        {
            var item = new GameServerDetailDto
            {
                Id = 1,
                ServerId = "srv-1",
                Name = "Server 1 Updated",
                ServiceName = "srv-1-svc",
                GameTypeRevisionId = 10,
                GameTypeKey = "valheim",
                Status = "Running",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false,
                Ports = new List<GameServerPortDto>(),
                Settings = new List<GameServerSettingDto>(),
                ResolvedPorts = new List<GameServerResolvedPortDto>(),
                ResolvedVolumes = new List<GameServerResolvedVolumeDto>(),
                ResolvedWebHosts = new List<GameServerResolvedWebHostDto>()
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(item));
            var result = await api.UpdateAsync("srv-1", new SaveGameServerRequestDto { Name = "Server 1 Updated", GameTypeRevisionId = 10 });

            Assert.NotNull(result);
            Assert.Equal("Server 1 Updated", result.Name);
        }

        [Fact]
        public async Task GameServersApi_DeleteAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            var api = new GameServersApi(CreateMockHttpClient(response));

            await api.DeleteAsync("srv-1", true);
        }

        [Fact]
        public async Task GameServersApi_ValidateAsync_DeserializesResponse()
        {
            var validation = new GameServerValidationResultDto
            {
                IsValid = true,
                Issues = new List<GameServerValidationIssueDto>()
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(validation));
            var result = await api.ValidateAsync(new SaveGameServerRequestDto { Name = "Server 1", GameTypeRevisionId = 10 });

            Assert.NotNull(result);
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task GameServersApi_PreviewAsync_DeserializesResponse()
        {
            var preview = new GameServerDeploymentPreviewDto
            {
                ServiceName = "srv-1-svc",
                ImageReference = "valheim:latest",
                Networks = new List<GameServerPreviewNetworkDto>(),
                Ports = new List<GameServerPreviewPortDto>(),
                EnvironmentVariables = new List<GameServerPreviewEnvironmentVariableDto>(),
                Volumes = new List<GameServerPreviewVolumeDto>()
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(preview));
            var result = await api.PreviewAsync(new SaveGameServerRequestDto { Name = "Server 1", GameTypeRevisionId = 10 });

            Assert.NotNull(result);
            Assert.Equal("srv-1-svc", result.ServiceName);
        }

        [Fact]
        public async Task GameServersApi_CheckPortAvailabilityAsync_DeserializesResponse()
        {
            var avail = new GameServerPortAvailabilityResultDto
            {
                Ports = new List<GameServerPortAvailabilityDto>()
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(avail));
            var result = await api.CheckPortAvailabilityAsync(new GameServerPortAvailabilityRequestDto
            {
                Ports = new List<GameServerPortAvailabilityRequestPortDto>()
            });

            Assert.NotNull(result);
            Assert.NotNull(result.Ports);
        }

        [Fact]
        public async Task GameServersApi_StartStopRestartRedeploy_DeserializesResponse()
        {
            var item = new GameServerDetailDto
            {
                Id = 1,
                ServerId = "srv-1",
                Name = "Server 1",
                ServiceName = "srv-1-svc",
                GameTypeRevisionId = 10,
                GameTypeKey = "valheim",
                Status = "Running",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false,
                Ports = new List<GameServerPortDto>(),
                Settings = new List<GameServerSettingDto>(),
                ResolvedPorts = new List<GameServerResolvedPortDto>(),
                ResolvedVolumes = new List<GameServerResolvedVolumeDto>(),
                ResolvedWebHosts = new List<GameServerResolvedWebHostDto>()
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(item));

            var started = await api.StartAsync("srv-1");
            Assert.Equal("srv-1", started.ServerId);

            var stopped = await api.StopAsync("srv-1");
            Assert.Equal("srv-1", stopped.ServerId);

            var restarted = await api.RestartAsync("srv-1");
            Assert.Equal("srv-1", restarted.ServerId);

            var redeployed = await api.RedeployAsync("srv-1");
            Assert.Equal("srv-1", redeployed.ServerId);
        }

        [Fact]
        public async Task GameServersApi_GetResourceHistoryAsync_DeserializesResponse()
        {
            var history = new[]
            {
                new GameServerResourceHistoryDto
                {
                    Id = 1,
                    ServerId = "srv-1",
                    Timestamp = DateTimeOffset.UtcNow,
                    CpuUsagePercent = 25.5,
                    MemoryUsageBytes = 500000000,
                    MemoryLimitBytes = 1000000000,
                    MemoryUsagePercent = 50.0
                }
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(history));
            var result = await api.GetResourceHistoryAsync("srv-1", null, null, 100);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("srv-1", result.First().ServerId);
        }

        [Fact]
        public async Task GameServersApi_GetLatestResourceAsync_DeserializesResponse()
        {
            var usage = new ServerResourceUsage
            {
                ServerId = "srv-1",
                CpuUsagePercent = 12.3,
                MemoryUsageBytes = 256000000
            };

            var api = new GameServersApi(CreateMockJsonHttpClient(usage));
            var result = await api.GetLatestResourceAsync("srv-1");

            Assert.NotNull(result);
            Assert.Equal("srv-1", result.ServerId);
            Assert.Equal(12.3, result.CpuUsagePercent);
        }

        #endregion

        #region GameTypesApi Tests

        [Fact]
        public async Task GameTypesApi_GetAllAsync_DeserializesResponse()
        {
            var items = new[]
            {
                new GameTypeListItemDto
                {
                    Id = 1,
                    Key = "valheim",
                    DisplayName = "Valheim",
                    Type = "docker",
                    IsActive = true,
                    RevisionCount = 1,
                    PublishedRevisionCount = 1,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            var api = new GameTypesApi(CreateMockJsonHttpClient(items));
            var result = await api.GetAllAsync(true);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("valheim", result.First().Key);
        }

        [Fact]
        public async Task GameTypesApi_GetByKeyAsync_DeserializesResponse()
        {
            var item = new GameTypeDetailDto
            {
                Id = 1,
                Key = "valheim",
                DisplayName = "Valheim",
                Type = "docker",
                IsActive = true,
                Revisions = new List<GameTypeRevisionDto>()
            };

            var api = new GameTypesApi(CreateMockJsonHttpClient(item));
            var result = await api.GetByKeyAsync("valheim");

            Assert.NotNull(result);
            Assert.Equal("valheim", result.Key);
        }

        [Fact]
        public async Task GameTypesApi_CreateAsync_DeserializesResponse()
        {
            var item = new GameTypeDetailDto
            {
                Id = 1,
                Key = "valheim",
                DisplayName = "Valheim",
                Type = "docker",
                IsActive = true,
                Revisions = new List<GameTypeRevisionDto>()
            };

            var api = new GameTypesApi(CreateMockJsonHttpClient(item, HttpStatusCode.Created));
            var result = await api.CreateAsync(new SaveGameTypeRequestDto { Key = "valheim", DisplayName = "Valheim", Type = "docker" });

            Assert.NotNull(result);
            Assert.Equal("valheim", result.Key);
        }

        [Fact]
        public async Task GameTypesApi_UpdateAsync_DeserializesResponse()
        {
            var item = new GameTypeDetailDto
            {
                Id = 1,
                Key = "valheim",
                DisplayName = "Valheim Updated",
                Type = "docker",
                IsActive = true,
                Revisions = new List<GameTypeRevisionDto>()
            };

            var api = new GameTypesApi(CreateMockJsonHttpClient(item));
            var result = await api.UpdateAsync("valheim", new SaveGameTypeRequestDto { Key = "valheim", DisplayName = "Valheim Updated", Type = "docker" });

            Assert.NotNull(result);
            Assert.Equal("Valheim Updated", result.DisplayName);
        }

        [Fact]
        public async Task GameTypesApi_DeleteAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            var api = new GameTypesApi(CreateMockHttpClient(response));

            await api.DeleteAsync("valheim");
        }

        [Fact]
        public async Task GameTypesApi_ExportAsync_DeserializesResponse()
        {
            var pkg = new PortableGameTypePackageDto
            {
                FormatVersion = "1.0",
                GameType = new PortableGameTypeDto
                {
                    Key = "valheim",
                    DisplayName = "Valheim",
                    Type = "docker",
                    IsActive = true,
                    Revisions = new List<PortableGameTypeRevisionDto>()
                }
            };

            var api = new GameTypesApi(CreateMockJsonHttpClient(pkg));
            var result = await api.ExportAsync("valheim");

            Assert.NotNull(result);
            Assert.Equal("1.0", result.FormatVersion);
            Assert.Equal("valheim", result.GameType.Key);
        }

        [Fact]
        public async Task GameTypesApi_ImportAsync_DeserializesResponse()
        {
            var item = new GameTypeDetailDto
            {
                Id = 1,
                Key = "valheim",
                DisplayName = "Valheim",
                Type = "docker",
                IsActive = true,
                Revisions = new List<GameTypeRevisionDto>()
            };

            var api = new GameTypesApi(CreateMockJsonHttpClient(item, HttpStatusCode.Created));
            var result = await api.ImportAsync(new PortableGameTypePackageDto
            {
                FormatVersion = "1.0",
                GameType = new PortableGameTypeDto
                {
                    Key = "valheim",
                    DisplayName = "Valheim",
                    Type = "docker",
                    IsActive = true,
                    Revisions = new List<PortableGameTypeRevisionDto>()
                }
            });

            Assert.NotNull(result);
            Assert.Equal("valheim", result.Key);
        }

        [Fact]
        public async Task GameTypesApi_RevisionsOperations_DeserializesResponse()
        {
            var rev = new GameTypeRevisionDto
            {
                Id = 10,
                VersionTag = "1.0",
                ImageReference = "valheim:latest",
                IsPublished = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Ports = new List<GameTypePortDto>(),
                Volumes = new List<GameTypeVolumeDto>(),
                SettingDefinitions = new List<GameTypeSettingDefinitionDto>(),
                WebHosts = new List<GameTypeWebHostDto>(),
                UiExtensions = new List<GameTypeUiExtensionDescriptorDto>()
            };

            var apiCreated = new GameTypesApi(CreateMockJsonHttpClient(rev, HttpStatusCode.Created));
            var added = await apiCreated.AddRevisionAsync("valheim", new SaveGameTypeRevisionRequestDto { VersionTag = "1.0", ImageReference = "valheim:latest" });
            Assert.Equal(10, added.Id);

            var apiOk = new GameTypesApi(CreateMockJsonHttpClient(rev, HttpStatusCode.OK));
            var updated = await apiOk.UpdateRevisionAsync("valheim", 10, new SaveGameTypeRevisionRequestDto { VersionTag = "1.0", ImageReference = "valheim:latest" });
            Assert.Equal(10, updated.Id);

            var published = await apiOk.PublishRevisionAsync("valheim", 10, new PublishRevisionRequestDto { SetAsCurrentRevision = true });
            Assert.Equal(10, published.Id);
        }

        [Fact]
        public async Task GameTypesApi_SetCurrentRevisionAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            var api = new GameTypesApi(CreateMockHttpClient(response));

            await api.SetCurrentRevisionAsync("valheim", 10);
        }

        [Fact]
        public async Task GameTypesApi_DetectionOperations_DeserializesResponse()
        {
            var detectResult = new GameTypeSetupDetectionResultDto
            {
                ImageReference = "valheim:latest",
                Ports = new List<DetectedPortDto>(),
                Volumes = new List<DetectedVolumeDto>(),
                Settings = new List<DetectedSettingDto>()
            };

            var api = new GameTypesApi(CreateMockJsonHttpClient(detectResult));
            var detected1 = await api.ScanTagAsync(new DetectGameTypeSetupRequestDto { ImageReference = "valheim:latest" });
            Assert.Equal("valheim:latest", detected1.ImageReference);

            var detected2 = await api.ScanTag2Async("valheim", new DetectGameTypeSetupRequestDto { ImageReference = "valheim:latest" });
            Assert.Equal("valheim:latest", detected2.ImageReference);

            var compareResult = new GameTypeSetupComparisonResultDto
            {
                Detection = detectResult,
                RevisionId = 10,
                RevisionVersionTag = "1.0",
                HasChanges = false,
                DigestChanged = false,
                AddedPorts = new List<string>(),
                RemovedPorts = new List<string>(),
                AddedVolumes = new List<string>(),
                RemovedVolumes = new List<string>()
            };

            var apiCompare = new GameTypesApi(CreateMockJsonHttpClient(compareResult));
            var compared = await apiCompare.CompareDetectionAsync("valheim", new CompareGameTypeSetupRequestDto { ImageReference = "valheim:latest" });
            Assert.NotNull(compared);
        }

        #endregion

        #region MountTypeConfigApi Tests

        [Fact]
        public async Task MountTypeConfigApi_GetAllAsync_DeserializesResponse()
        {
            var items = new[]
            {
                new MountTypeConfigDto
                {
                    Key = "local",
                    DisplayName = "Local Volume",
                    Description = "Local storage",
                    VolumeNameFormat = "{ServerId}_{MountType}_{VolumeName}",
                    Options = new Dictionary<string, string>()
                }
            };

            var api = new MountTypeConfigApi(CreateMockJsonHttpClient(items));
            var result = await api.GetAllAsync();

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("local", result.First().Key);
        }

        [Fact]
        public async Task MountTypeConfigApi_GetByKeyAsync_DeserializesResponse()
        {
            var item = new MountTypeConfigDto
            {
                Key = "nfs",
                DisplayName = "NFS Share",
                Description = "NFS network storage",
                VolumeNameFormat = "{ServerId}_{MountType}_{VolumeName}",
                Options = new Dictionary<string, string>()
            };

            var api = new MountTypeConfigApi(CreateMockJsonHttpClient(item));
            var result = await api.GetAsync("nfs");

            Assert.NotNull(result);
            Assert.Equal("nfs", result.Key);
        }

        [Fact]
        public async Task MountTypeConfigApi_SaveAsync_DeserializesResponse()
        {
            var item = new MountTypeConfigDto
            {
                Key = "nfs",
                DisplayName = "NFS Share",
                Description = "NFS network storage",
                VolumeNameFormat = "{ServerId}_{MountType}_{VolumeName}",
                Options = new Dictionary<string, string>()
            };

            var api = new MountTypeConfigApi(CreateMockJsonHttpClient(item));
            var result = await api.SaveAsync("nfs", item);

            Assert.NotNull(result);
            Assert.Equal("nfs", result.Key);
        }

        [Fact]
        public async Task MountTypeConfigApi_DeleteAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            var api = new MountTypeConfigApi(CreateMockHttpClient(response));

            await api.DeleteAsync("nfs");
        }

        #endregion

        #region GameServersFilesApi Tests

        [Fact]
        public async Task GameServersFilesApi_ListAsync_DeserializesResponse()
        {
            var items = new[]
            {
                new FileItemDto { Name = "server.properties", Path = "/server.properties", IsDirectory = false, Size = 1024 }
            };

            var api = new GameServersFilesApi(CreateMockJsonHttpClient(items));
            var result = await api.ListAsync("srv-1", "/data", "");

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("server.properties", result.First().Name);
        }

        [Fact]
        public async Task GameServersFilesApi_GetContentAsync_DeserializesResponse()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("\"file content text\"", Encoding.UTF8, "application/json")
            };
            var api = new GameServersFilesApi(CreateMockHttpClient(response));
            var result = await api.GetContentAsync("srv-1", "/data", "test.txt");

            Assert.Equal("file content text", result);
        }

        [Fact]
        public async Task GameServersFilesApi_SaveContentAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var api = new GameServersFilesApi(CreateMockHttpClient(response));

            await api.SaveContentAsync("srv-1", "/data", "test.txt", new SaveFileContentRequestDto { Content = "new data" });
        }

        [Fact]
        public async Task GameServersFilesApi_DownloadAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            };
            var api = new GameServersFilesApi(CreateMockHttpClient(response));

            await api.DownloadAsync("srv-1", "/data", "test.bin");
        }

        [Fact]
        public async Task GameServersFilesApi_CreateDirectoryAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var api = new GameServersFilesApi(CreateMockHttpClient(response));

            await api.CreateDirectoryAsync("srv-1", "/data", "newfolder");
        }

        [Fact]
        public async Task GameServersFilesApi_DeleteAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var api = new GameServersFilesApi(CreateMockHttpClient(response));

            await api.DeleteAsync("srv-1", "/data", "oldfile.txt", false);
        }

        [Fact]
        public async Task GameServersFilesApi_UploadAsync_CompletesSuccessfully()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var api = new GameServersFilesApi(CreateMockHttpClient(response));

            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var fileParam = new FileParameter(stream, "upload.txt", "text/plain");

            await api.UploadAsync("srv-1", "/data", "subfolder", fileParam);
        }

        #endregion

        #region PortApi Tests

        [Fact]
        public async Task PortApi_CheckAsync_DeserializesResponse()
        {
            var api = new PortApi(CreateMockJsonHttpClient(true));
            var result = await api.CheckAsync("tcp", 25565);

            Assert.True(result);
        }

        #endregion

        #region FileParameter and Exception Tests

        [Fact]
        public void FileParameter_Properties_SetCorrectly()
        {
            using var stream = new MemoryStream(new byte[] { 42 });
            var fp1 = new FileParameter(stream);
            Assert.Equal(stream, fp1.Data);
            Assert.Null(fp1.FileName);
            Assert.Null(fp1.ContentType);

            var fp2 = new FileParameter(stream, "test.png", "image/png");
            Assert.Equal("test.png", fp2.FileName);
            Assert.Equal("image/png", fp2.ContentType);
        }

        [Fact]
        public void ApiExceptions_CanBeInstantiated()
        {
            var headers = new Dictionary<string, IEnumerable<string>>();
            var ex1 = new GameServersApiException("error", 400, "response", headers, null);
            Assert.Equal(400, ex1.StatusCode);
            Assert.Equal("response", ex1.Response);

            var ex2 = new GameServersApiException<ProblemDetails>("error", 400, "response", headers, new ProblemDetails { Title = "Err" }, null);
            Assert.NotNull(ex2.Result);
            Assert.Equal("Err", ex2.Result.Title);

            var ex3 = new GameTypesApiException("error", 404, "response", headers, null);
            Assert.Equal(404, ex3.StatusCode);

            var ex4 = new MountTypeConfigApiException("error", 500, "response", headers, null);
            Assert.Equal(500, ex4.StatusCode);

            var ex5 = new PortApiException("error", 400, "response", headers, null);
            Assert.Equal(400, ex5.StatusCode);

            var ex6 = new GameServersFilesApiException("error", 404, "response", headers, null);
            Assert.Equal(404, ex6.StatusCode);
        }

        #endregion
    }
}
