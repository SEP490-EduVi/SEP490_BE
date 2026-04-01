using System.Text.Json.Serialization;

namespace EduVi.Contracts.DTOs.Games.Response;

public class PlayableGameResponse
{
    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("settings")]
    public GameSettings Settings { get; set; } = new();

    [JsonPropertyName("scene")]
    public GameScene Scene { get; set; } = new();

    [JsonPropertyName("payload")]
    public object Payload { get; set; } = new();
}

public class GameSettings
{
    [JsonPropertyName("mirror")]
    public bool Mirror { get; set; } = true;

    [JsonPropertyName("timeLimitSec")]
    public int TimeLimitSec { get; set; }

    [JsonPropertyName("hoverHoldMs")]
    public int HoverHoldMs { get; set; }

    [JsonPropertyName("pinchThreshold")]
    public double PinchThreshold { get; set; }
}

public class GameScene
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("backgroundUrl")]
    public string? BackgroundUrl { get; set; }
}

public class NormalizedRect
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("w")]
    public double W { get; set; }

    [JsonPropertyName("h")]
    public double H { get; set; }
}

public class HoverChoice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("zone")]
    public NormalizedRect Zone { get; set; } = new();
}

public class HoverSelectPlayable
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<HoverChoice> Choices { get; set; } = new();

    [JsonPropertyName("correctChoiceId")]
    public string CorrectChoiceId { get; set; } = string.Empty;
}

public class DraggableItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public NormalizedPoint Start { get; set; } = new();

    [JsonPropertyName("size")]
    public NormalizedSize Size { get; set; } = new();
}

public class NormalizedPoint
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

public class NormalizedSize
{
    [JsonPropertyName("w")]
    public double W { get; set; }

    [JsonPropertyName("h")]
    public double H { get; set; }
}

public class DropZone
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("zone")]
    public NormalizedRect Zone { get; set; } = new();

    [JsonPropertyName("acceptsItemId")]
    public string AcceptsItemId { get; set; } = string.Empty;
}

public class DragDropPlayable
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<DraggableItem> Items { get; set; } = new();

    [JsonPropertyName("dropZones")]
    public List<DropZone> DropZones { get; set; } = new();
}
