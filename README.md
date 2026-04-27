Clinic Appointments REST API with ADO.NET
In this exercise you will build an ASP.NET Core Web API based on controllers. The goal is to practice SQL Server communication without Entity Framework: SqlConnection, SqlCommand, SqlDataReader, SQL parameters, DTOs, and basic CRUD operations.

Business Context
A medical clinic needs a small backend for managing patient appointments. The system stores patients, doctors, doctor specializations, and appointments. A receptionist should be able to list appointments, inspect one appointment, create a new appointment, update appointment data, and delete an appointment that should no longer remain as an active booking.

Important. Do not use Entity Framework in this exercise. All database communication must be implemented manually with ADO.NET.

What You Need to Build
Create an ASP.NET Core Web API in C# with a controller for the Appointment resource. Data must be stored in SQL Server. Use Microsoft.Data.SqlClient to execute database commands.

SQL Files
The database scripts are stored in apbd/wykład_6/zadanie_1_przychodnia_sql.

File Description 01_create_and_seed_clinic.sql Creates ClinicAdoNet, tables, and sample data. 02_drop_clinic_tables.sql Drops only tables, leaving the database itself. 03_drop_clinic_database.sql Drops the whole ClinicAdoNet database.
Database Model
Specializations — doctor specializations,
Doctors — doctors assigned to specializations,
Patients — clinic patients,
Appointments — patient appointments.


Required DTOs
AppointmentListDto for the appointment list,
AppointmentDetailsDto for one appointment,
CreateAppointmentRequestDto for appointment creation,
UpdateAppointmentRequestDto for appointment updates,
ErrorResponseDto for error responses.
Endpoint Scope
Method Endpoint Description GET /api/appointments Returns appointments with basic patient data. GET /api/appointments/{idAppointment} Returns details of one appointment. POST /api/appointments Creates a new appointment. PUT /api/appointments/{idAppointment} Updates an existing appointment. DELETE /api/appointments/{idAppointment} Deletes an appointment.
Most important. Endpoints must return DTOs, and all values coming from route, query string, or request body must be passed to SQL through parameters.
