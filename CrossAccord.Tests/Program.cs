// See https://aka.ms/new-console-template for more information

using CrossAccord.Sample;
using CrossAccord.Tests.Patches;

var classPatch = new SimpleClassPatch();

var simpleClass = new SimpleClass();

simpleClass.Run("What?!");