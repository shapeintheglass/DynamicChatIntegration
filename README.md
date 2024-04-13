# Dynamic Chat Integration Bot

Simple prototype bot that allows modifying an .ini file in response to chat commands.

## Quick start

How to run the bot with test credentials.

### Setup:

1. Go to https://twitchtokengenerator.com/quick/JqwV9wWVq0 and log in with your Twitch credentials.
2. Modify appsettings.json to set "AccessToken" as the access token you receive in step 1.
3. Modify appsettings.json to set "Channel" as the Twitch channel whose chat the bot should listen to.
4. Run DynamicChatIntegration.exe.

Example of what appsettings.json looks like when configured with these values:

```
{
  "Settings": {
    "___COMMENT_ACCESS_TOKEN": "Get a test access token here: https://twitchtokengenerator.com/quick/JqwV9wWVq0",
    "AccessToken": "vmxrvshmedmakpvgznrz8gczsmesgp",

    "___COMMENT_CHANNEL": "Define the channel to listen to here",
    "Channel": "kidaXV",
...
```

### Usage:

At startup, the bot will look for a file called "original.ini" and make a working copy in a separate location call "modified.ini". The modified file will be the one it will continue to modify, relying on the original as a backup.

By default, the bot will respond to two types of commands: Set and Reset.

To set foo = bar:

`!ini foo=bar`

To set foo = bar in section [foobar]:

`!ini [foobar] foo=bar`

To set the value foo to an empty string:

`!ini foo=`

To reset the entire state of the file to the original state:

`!ini reset`

## Customization

The value of the original and modified ini files can be set in appsettings.json as `OriginalIniPath` and `ModifiedIniPath`, respectively.

The appsettings.json file allows defining additional commands that are a simple substitution for the ones above.

For instance, if you want to define `!foobar` as shorthand for `!ini foo=bar`, you can set the appsettings.json file like this:

```
...

    "___COMMENT_COMMANDS": "Define custom commands here.",
    "Commands": [
      [ "!resetini", "!ini reset" ],
      [ "!foobar", "!ini foo=bar" ]
    ],
...
```

It is also possible to adjust the underlying syntax, such as the default value of the `!ini` prefix:

 * CommandPrefix = Prefix that must occur before all debug commands.
 * CommandReset = Word to use as the reset command.
 * CommandDelimiter = String to use as the delimiter between the prefix and follow-up commands.
 * CommandSetRegex = Regex that labels the section, property, and value to set.
 * CommandGetRegex = Regex that labels the section and property to get.

## Permissions

To only allow trusted users to run the underlying commands, set `"RestrictDebugCommandsToAllowedUsers": true` and define allowed users by their username in `"DebugCommandsAllowedUsers"`.

Example:
```
...
    "___COMMENT_PERMISSIONS": "Use these settings to restrict debug commands to specific users",
    "RestrictDebugCommandsToAllowedUsers": true,
    "DebugCommandsAllowedUsers": [ "kidaXV", "SHAPE_IN_THE_GLASS" ],
...
```

To allow the bot itself to post messages in the chat, set `PostResponsesInChat` to true. This can be useful for debugging purposes.

## Debugging

The application offers two flags for debugging. `--verbose` adds verbose logging.

`--debug` will change the mode so that commands are read directly from the command line rather than from Twitch chat. This can be useful for testing custom commands.

Example usage: `.\DynamicChatIntegration.exe --verbose --debug`