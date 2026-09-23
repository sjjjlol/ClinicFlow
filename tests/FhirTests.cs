using System.Text.Json;
using ClinicFlow;
using ClinicFlow.Fhir;
using ClinicFlow.Scheduling;
using Xunit;
namespace ClinicFlow.Tests;
public class FhirTests
{
    [Theory][InlineData("Pending","pending")][InlineData("Confirmed","booked")][InlineData("Cancelled","cancelled")]
    public void A20_AppointmentMappingUsesR4FieldsAndUtc(string domain,string fhir)
    {
        var a=new Appointment {Id="767cfcdc-42d9-407e-b331-2b7c75a670f3",PatientId=1,ResourceId=2,StartUtc=new(2030,1,1,1,0,0),EndUtc=new(2030,1,1,2,0,0),Status=domain,Version=7};
        var json=JsonSerializer.SerializeToElement(FhirAdapter.AppointmentResource(a,"Room B"),SchedulingService.Json);
        Assert.Equal("Appointment",json.GetProperty("resourceType").GetString());Assert.Equal(fhir,json.GetProperty("status").GetString());Assert.Equal("7",json.GetProperty("meta").GetProperty("versionId").GetString());Assert.EndsWith("Z",json.GetProperty("start").GetString());Assert.Equal(60,json.GetProperty("minutesDuration").GetInt32());Assert.Equal("Patient/1",json.GetProperty("participant")[0].GetProperty("actor").GetProperty("reference").GetString());Assert.Equal(2,json.GetProperty("participant").GetArrayLength());
    }
    [Fact]public void A20_PatientMappingHasIdentifierAndName()
    {
        var json=JsonSerializer.SerializeToElement(FhirAdapter.PatientResource(new Patient{Id=1,Name="Test",Identifier="DEMO-001"}));Assert.Equal("Patient",json.GetProperty("resourceType").GetString());Assert.Equal("DEMO-001",json.GetProperty("identifier")[0].GetProperty("value").GetString());Assert.Equal("Test",json.GetProperty("name")[0].GetProperty("text").GetString());
    }
}
