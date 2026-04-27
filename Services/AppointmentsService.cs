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
        
        if (!await PatientExistsAsync(connection, appointment.IdPatient)) return null;
        if (!await DoctorExistsAsync(connection, appointment.IdDoctor)) return null;
        
        //check date 
        if (appointment.AppointmentDate < DateTime.Now)
            return null;
        
        //reason validation
        if (string.IsNullOrWhiteSpace(appointment.Reason) || appointment.Reason.Length > 250)
            return null;
        //conflicts
        if (await HasConflictAsync(connection, appointment.IdDoctor, appointment.AppointmentDate)) return null;
        
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
    
    
    
    public async Task<AppointmentDto?> UpdateAppointment(int id, UpdateAppointmentRequestDto request)
{
    await using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();

    //businessRules
    
    // Check if appointment exists 
    var existing = await GetAppointmentByIdAsync(id);
    if (existing == null) return null;
    
    if (!await PatientExistsAsync(connection, request.IdPatient)) return null;
    if (!await DoctorExistsAsync(connection, request.IdDoctor)) return null;

    // Validate status
    if (!IsValidStatus(request.Status)) return null;

    //  Validate reason
    if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 250)
        return null;

    // Prevent date change if already completed
    if (existing.Status == "Completed" &&
        existing.AppointmentDate != request.AppointmentDate)
        return null;

    // Check conflict 
    if ((existing.AppointmentDate != request.AppointmentDate ||
         existing.IdDoctor != request.IdDoctor) &&
        await HasConflictExcludingSelfAsync(connection, id, request.IdDoctor, request.AppointmentDate))
        return null;

    //  Update
    await using var cmd = new SqlCommand(@"
        UPDATE dbo.Appointments
        SET 
            IdPatient = @IdPatient,
            IdDoctor = @IdDoctor,
            AppointmentDate = @AppointmentDate,
            Status = @Status,
            Reason = @Reason,
            InternalNotes = @InternalNotes
        WHERE IdAppointment = @IdAppointment;
    ", connection);

    cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
    cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
    cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = request.AppointmentDate;
    cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = request.Status;
    cmd.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = request.Reason;
    cmd.Parameters.Add("@InternalNotes", SqlDbType.NVarChar).Value = request.InternalNotes;
    cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = id;

    await cmd.ExecuteNonQueryAsync();
    
    return await GetAppointmentByIdAsync(id);
}
    
    private bool IsValidStatus(string status)
    {
        return status == "Scheduled" ||
               status == "Completed" ||
               status == "Cancelled";
    }
    
    private async Task<bool> PatientExistsAsync(SqlConnection connection, int idPatient)
    {
        await using var cmd = new SqlCommand(@"
        SELECT 1 FROM Patients 
        WHERE IdPatient = @IdPatient AND IsActive = 1;
        ", connection);

        cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = idPatient;

        return await cmd.ExecuteScalarAsync() != null;
    }
    
    private async Task<bool> DoctorExistsAsync(SqlConnection connection, int idDoctor)
    {
        await using var cmd = new SqlCommand(@"
        SELECT 1 FROM Doctors 
        WHERE IdDoctor = @IdDoctor AND IsActive = 1;
        ", connection);

        cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;

        return await cmd.ExecuteScalarAsync() != null;
    }
    private async Task<bool> HasConflictAsync(SqlConnection connection, int doctorId, DateTime date)
    {
        await using var cmd = new SqlCommand(@"
        SELECT 1
        FROM Appointments
        WHERE IdDoctor = @IdDoctor
          AND AppointmentDate = @AppointmentDate
          AND Status = 'Scheduled';
        ", connection);

        cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = doctorId;
        cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = date;

        return await cmd.ExecuteScalarAsync() != null;
    }
    
    private async Task<bool> HasConflictExcludingSelfAsync(
        SqlConnection connection,
        int appointmentId,
        int doctorId,
        DateTime date)
    {
        await using var cmd = new SqlCommand(@"
        SELECT 1
        FROM Appointments
        WHERE IdDoctor = @IdDoctor
          AND AppointmentDate = @AppointmentDate
          AND IdAppointment <> @IdAppointment
          AND Status = 'Scheduled';
        ", connection);

        cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = doctorId;
        cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = date;
        cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = appointmentId;

        return await cmd.ExecuteScalarAsync() != null;
    }
    
    
}