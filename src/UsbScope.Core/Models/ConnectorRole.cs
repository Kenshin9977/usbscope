namespace UsbScope.Core.Models;

public enum ConnectorRole
{
    Unknown,
    DownstreamFacingPort,
    UpstreamFacingPort,
    DualRoleData,
}

public enum PowerRole
{
    Unknown,
    Source,
    Sink,
    DualRolePower,
}
