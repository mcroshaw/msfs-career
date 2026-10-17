using MsfsCareer.Cli;

return args switch
{
    ["replay", var path] => Commands.Replay(path),
    ["record"] => Commands.Record(),
    _ => Commands.Usage(),
};
