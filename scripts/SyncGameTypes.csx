#!/usr/bin/env dotnet-script
#r "nuget: System.Text.Json, 9.0.0"

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

var sourceBaseUrl = "http://192.168.10.50:5164";
var targetBaseUrl = "http://192.168.10.50:5163";

var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

Console.WriteLine("🔄 Starting GameType synchronization...");
Console.WriteLine($"   Source: {sourceBaseUrl}");
Console.WriteLine($"   Target: {targetBaseUrl}");
Console.WriteLine();

try
{
    // 1. Get all GameTypes from source
    Console.WriteLine("📥 Fetching GameTypes from source...");
    var sourceGameTypes = await httpClient.GetFromJsonAsync<JsonElement>($"{sourceBaseUrl}/api/gametype");
    
    if (sourceGameTypes.ValueKind != JsonValueKind.Array)
    {
        Console.WriteLine("❌ Failed to get GameTypes from source");
        return 1;
    }

    var gameTypeCount = sourceGameTypes.GetArrayLength();
    Console.WriteLine($"✅ Found {gameTypeCount} GameTypes on source");
    Console.WriteLine();

    int successCount = 0;
    int errorCount = 0;

    // 2. Sync each GameType
    foreach (var gameType in sourceGameTypes.EnumerateArray())
    {
        var key = gameType.GetProperty("key").GetString();
        var displayName = gameType.GetProperty("displayName").GetString();
        
        Console.WriteLine($"🔄 Syncing: {displayName} ({key})");

        try
        {
            // 2a. Create/Update GameType on target
            var gameTypeJson = JsonContent.Create(gameType);
            var response = await httpClient.PostAsync($"{targetBaseUrl}/api/gametype", gameTypeJson);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   ⚠️  GameType sync failed: {response.StatusCode} - {error}");
                errorCount++;
                continue;
            }
            
            Console.WriteLine($"   ✅ GameType synced");

            // 2b. Get extended metadata from source
            try
            {
                var extendedMetadata = await httpClient.GetFromJsonAsync<JsonElement>(
                    $"{sourceBaseUrl}/api/gametype/{key}/extended-metadata");
                
                // 2c. Save extended metadata to target
                var metadataJson = JsonContent.Create(extendedMetadata);
                var metadataResponse = await httpClient.PutAsync(
                    $"{targetBaseUrl}/api/gametype/{key}/extended-metadata", 
                    metadataJson);
                
                if (metadataResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"   ✅ Extended metadata synced");
                }
                else
                {
                    Console.WriteLine($"   ⚠️  Extended metadata sync failed: {metadataResponse.StatusCode}");
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"   ℹ️  No extended metadata found");
            }
            
            successCount++;
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error: {ex.Message}");
            errorCount++;
            Console.WriteLine();
        }
    }

    // Summary
    Console.WriteLine("=" .PadRight(50, '='));
    Console.WriteLine($"✅ Successfully synced: {successCount}/{gameTypeCount}");
    if (errorCount > 0)
    {
        Console.WriteLine($"❌ Failed: {errorCount}/{gameTypeCount}");
    }
    Console.WriteLine("=" .PadRight(50, '='));

    return errorCount > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}
