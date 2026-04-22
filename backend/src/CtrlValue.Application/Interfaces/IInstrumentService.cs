using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Interfaces;

public interface IInstrumentService
{
    // Instrument CRUD
    Task<List<InstrumentDto>> GetInstrumentsAsync(InstrumentType? type = null);
    Task<InstrumentDto?> GetInstrumentByIdAsync(Guid id);
    Task<InstrumentDto?> GetInstrumentBySymbolAsync(string symbol);
    Task<InstrumentDto> CreateInstrumentAsync(CreateInstrumentRequest request);
    Task<InstrumentDto> UpdateInstrumentAsync(Guid id, UpdateInstrumentRequest request);
    Task DeleteInstrumentAsync(Guid id);
}
