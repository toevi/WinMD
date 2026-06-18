namespace WinMD.Models;

/// <summary>
/// A single entry in the Markdown help cheatsheet: a marker name, its syntax,
/// a short description and an example snippet that gets rendered to show the result.
/// </summary>
/// <param name="Title">Human-readable name of the marker (e.g. "Bold").</param>
/// <param name="Syntax">The literal Markdown syntax (e.g. "**text**").</param>
/// <param name="Description">What the marker does.</param>
/// <param name="Example">A Markdown snippet rendered as a live example of the result.</param>
public sealed record MarkdownTip(string Title, string Syntax, string Description, string Example);
