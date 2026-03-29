namespace StartRef.Desktop.Models;

public class DeleteTodayDataResponse
{
    public string Date { get; set; } = "";
    public int DeletedRunners { get; set; }
    public int DeletedCompetitions { get; set; }
    public int DeletedClasses { get; set; }
    public int DeletedClubs { get; set; }
}
