using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAgentSettingService
{
    Task<string?> GetAsync(string key);
    Task<Dictionary<string, string>> GetAllAsync();
    Task SetAsync(string key, string value);
}
