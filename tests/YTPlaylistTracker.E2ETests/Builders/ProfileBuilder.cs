using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.E2ETests.Builders;

internal sealed class ProfileBuilder
{
    private int _id = 1;
    private string _name = "Test Profile";
    private bool _isDefault = true;
    private bool _isOffline = true;

    public ProfileBuilder WithId(int id) { _id = id; return this; }
    public ProfileBuilder WithName(string name) { _name = name; return this; }
    public ProfileBuilder AsDefault(bool isDefault = true) { _isDefault = isDefault; return this; }
    public ProfileBuilder AsOnline() { _isOffline = false; return this; }

    public Profile Build() => new()
    {
        Id = _id,
        Name = _name,
        IsDefault = _isDefault,
        IsOffline = _isOffline,
    };
}
