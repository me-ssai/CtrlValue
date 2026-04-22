using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentSettingService : IAgentSettingService
{
    private readonly AppDbContext _db;

    public AgentSettingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _db.AgentSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        return await _db.AgentSettings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    public async Task SetAsync(string key, string value)
    {
        var existing = await _db.AgentSettings
            .FirstOrDefaultAsync(s => s.Key == key);

        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            _db.AgentSettings.Add(new AgentSetting
            {
                Key = key,
                Value = value,
                TenantId = ""
            });
        }

        await _db.SaveChangesAsync();
    }
}
