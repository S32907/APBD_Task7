using System.Data;
using Microsoft.Data.SqlClient;
using Tutorial7.DTOs;

namespace Tutorial7.Services;

public class AppointmentsService : IAppointmentsService
{
    private readonly string _connectionString;

    public AppointmentsService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }
    
    public async Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync()
    {
        var query = "SELECT " +
                    "a.IdAppointment, " +
                    "a.AppointmentDate, " +
                    "a.Reason, " +
                    "a.Status, " +
                    "p.FirstName, " +
                    "p.LastName," +
                    "p.Email " +
                    "FROM Appointments a " +
                    "JOIN Patients p ON a.IdPatient = p.IdPatient";
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection = connection;
        command.CommandText = query;

        var reader = await command.ExecuteReaderAsync();
        
        var appointments = new List<AppointmentListDto>();
        while (await reader.ReadAsync())
        {
            var appointment = new AppointmentListDto()
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Reason = reader.GetString(2),
                Status = reader.GetString(3),
                PatientFullName = reader.GetString(4) + " " + reader.GetString(5),
                PatientEmail = reader.GetString(6),
            };
            appointments.Add(appointment);
        }
        
        return appointments;
    }

    public async Task<AppointmentDto> GetAppointmentByIdAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(@"SELECT
    IdAppointment,
       IdPatient,
       IdDoctor,
       AppointmentDate,
       Status,
       Reason,
       InternalNotes,
       CreatedAt FROM dbo.Appointments WHERE IdAppointment = @IdAppointment;
", connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = id;
        var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var appointment = new AppointmentDto
            {
                IdAppointment = reader.GetInt32(0),
                IdPatient = reader.GetInt32(1),
                IdDoctor = reader.GetInt32(2),
                AppointmentDate = reader.GetDateTime(3),
                Status = reader.GetString(4),
                Reason = reader.GetString(5),
                InternalNotes = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7)
            };
            return appointment;
        }
        return null;
    }
}