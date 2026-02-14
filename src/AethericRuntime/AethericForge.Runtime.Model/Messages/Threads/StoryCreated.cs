using AethericForge.Runtime.Model.Threads;

namespace AethericForge.Runtime.Model.Messages.Threads;

public sealed class StoryCreated(Story story) : Message<Story>(story) { }
