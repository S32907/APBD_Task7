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

    public async Task<AppointmentDto> CreateAppointment(CreateAppointmentRequestDto appointment)
    {
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        //businessRules
        
        //check if patient exist
        await using var patientCmd = new SqlCommand(@"
        SELECT 1 
        FROM Patients 
        WHERE IdPatient = @IdPatient AND IsActive = 1;
        ", connection);

        patientCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = appointment.IdPatient;

        var patientExists = await patientCmd.ExecuteScalarAsync();

        if (patientExists == null)
            return null;
        
        //check if doctor exists 
        await using var doctorCmd = new SqlCommand(@"
        SELECT 1 
        FROM Doctors 
        WHERE IdDoctor = @IdDoctor AND IsActive = 1;
        ", connection);

        doctorCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;

        var doctorExists = await doctorCmd.ExecuteScalarAsync();

        if (doctorExists == null)
            return null;
        
        //check date 
        if (appointment.AppointmentDate < DateTime.Now)
            return null;
        
        //reason validation
        if (string.IsNullOrWhiteSpace(appointment.Reason) || appointment.Reason.Length > 250)
            return null;
        
        //Doctor availability 
        await using var conflictCmd = new SqlCommand(@"
        SELECT 1
        FROM Appointments
        WHERE IdDoctor = @IdDoctor
         AND AppointmentDate = @AppointmentDate
        AND Status = 'Scheduled';
        ", connection);

        conflictCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;
        conflictCmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = appointment.AppointmentDate;

        var conflict = await conflictCmd.ExecuteScalarAsync();

        if (conflict != null)
            return null;
        
        //inserting new appointment 
        await using var command = new SqlCommand(@"
        INSERT INTO dbo.Appointments
        (IdPatient, IdDoctor, AppointmentDate, Status, Reason, InternalNotes, CreatedAt)
        VALUES
        (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason, default, @CreatedAt);

        SELECT CAST(SCOPE_IDENTITY() AS int);
        ", connection);
        
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = appointment.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = appointment.AppointmentDate;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = appointment.Reason;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTime).Value = DateTime.UtcNow;
        
        //saving its id
        var newId = (int)await command.ExecuteScalarAsync();
        
        //selecting new appointment
        await using var selectCommand = new SqlCommand(@"
        SELECT 
            IdAppointment,
            IdPatient,
            IdDoctor,
            AppointmentDate,
            Status,
            Reason,
            InternalNotes,
            CreatedAt
        FROM dbo.Appointments
        WHERE IdAppointment = @IdAppointment;
        ", connection);
        
        //using saved id
        selectCommand.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = newId;

        await using var reader = await selectCommand.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new AppointmentDto
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
        }
        return null;
    }
}