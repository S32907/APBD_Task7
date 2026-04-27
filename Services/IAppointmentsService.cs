using Tutorial7.DTOs;

namespace Tutorial7.Services;

public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(); 
    Task<AppointmentDto> GetAppointmentByIdAsync(int id);
    Task<AppointmentDto> CreateAppointment(CreateAppointmentRequestDto appointment);
    Task<AppointmentDto?> UpdateAppointment(int id, UpdateAppointmentRequestDto request);
}