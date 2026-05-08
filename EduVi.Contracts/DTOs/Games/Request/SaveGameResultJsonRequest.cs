using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EduVi.Contracts.DTOs.Games.Request;

public class SaveGameResultJsonRequest
{
    [JsonPropertyName("resultJson")]
    [Required(ErrorMessage = "ResultJson không được để trống")]
    public JsonElement ResultJson { get; set; }
}
