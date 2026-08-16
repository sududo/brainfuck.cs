using System;
using System.Collections;
using System.Collections.Generic;
using Brainfuck;

public static class Program {
  public static int Main(string[] args){
    FileDataStream stream = new("./test.bf");
    Lexer lexer = new(stream);
    Parser.Result result = Parser.Parse(lexer);
    result.Run();
    return 0;
  }
}

namespace Brainfuck{
  public interface IDataStream {
    public string? GetNextLine();
  }

  public class FileDataStream : IDataStream {
    private StreamReader fileReader;

    public FileDataStream(string filePath){
      fileReader = new StreamReader(filePath);
    }

    public string? GetNextLine() => fileReader.ReadLine();

  }

  [Serializable]
  public class InvalidBracketException : Exception {
    public InvalidBracketException() : base() { }
    public InvalidBracketException(string message) : base(message) { }
    public InvalidBracketException(string message, Exception inner) : base(message, inner) { }
  }

  public class Lexer {
    public List<Token> tokens;

    private Lexer() => tokens = new();

    public Lexer(IDataStream dataStream){
      List<(int line, int column)> brackets = new();
      int currentLine = 0;
      tokens = new();
      string? input;
      for(;;){
        input = dataStream.GetNextLine();
        if(input is null) break;
        currentLine++;
        string line = input;

        for(int i = 0;i < line.Length;i++){
          if(Enum.IsDefined(typeof(Token), (int)line[i])) tokens.Add((Token)(int)line[i]);
          else continue;

          if(tokens[^1] == Token.LoopStart) brackets.Add((currentLine, i + 1));
          else if(tokens[^1] == Token.LoopEnd){
            if(brackets.Count == 0)
              throw new InvalidBracketException($"line {currentLine}, col {i + 1} : Extraneous closing brace");
            brackets.RemoveAt(brackets.Count - 1);
          }
        }
      } 
      if(brackets.Count != 0)
        throw new InvalidBracketException($@"No closing brace to match on at line {brackets[^1].line},
            col {brackets[^1].column}");
    }
  }

  public enum Token {
    ValueInc = '+',
    ValueDec = '-',
    PointerInc = '>',
    PointerDec = '<',
    LoopStart = '[',
    LoopEnd = ']',
    Output = '.',
    Input = ','
  }

  public class InterpretState {
    public byte[] memory;
    public int pointer;

    public InterpretState(){
      memory = new byte[2048];
      pointer = 0;
    }
  }

  public interface IExpression {
    public void Run(ref InterpretState state);
  }

  public class Atom : IExpression {
    Token type;

    public Atom(Token type) => this.type = type;

    public void Run(ref InterpretState state){
      switch(type){
      case Token.ValueInc:
        state.memory[state.pointer]++;
        break;

      case Token.ValueDec:
        state.memory[state.pointer]--;
        break;

      case Token.PointerInc:
        state.pointer++;
        if(state.pointer == state.memory.Length) state.pointer = 0;
        break;

      case Token.PointerDec:
        state.pointer--;
        if(state.pointer == -1) state.pointer = state.memory.Length - 1;
        break;

      case Token.Output:
        Console.Write((char)state.memory[state.pointer]);
        break;

      case Token.Input:
        state.memory[state.pointer] = (byte)Console.Read();
        break;
      }
    }
  }

  public class Loop : IExpression {
    List<IExpression> contents;

    public Loop(List<IExpression> contents) => this.contents = contents;

    public void Run(ref InterpretState state){

      while(state.memory[state.pointer] != 0){
        for(int i = 0;i < contents.Count;i++) contents[i].Run(ref state);
      }
    }
  }
  
  public class Parser {

    public class Result {
      List<IExpression> data;

      public Result(List<IExpression> data) => this.data = data;

      public void Run(){
        InterpretState state = new();
        state.memory = new byte[2048];
        state.pointer = 0;
        for(int i = 0;i < data.Count;i++) data[i].Run(ref state);
      }
    }

    public static Result Parse(Lexer lexer){
      List<IExpression> data = new();

      for(int i = 0;i < lexer.tokens.Count;i++){
        if(lexer.tokens[i] == Token.LoopStart) data.Add(ParseLoop(lexer, ref i));
        else data.Add(new Atom(lexer.tokens[i]));
      }

      return new Result(data);
    }

    private static Loop ParseLoop(Lexer lexer, ref int index){
      if(lexer.tokens[index++] != Token.LoopStart) throw new Exception("Never");
      List<IExpression> output = new();

      for(;index < lexer.tokens.Count;index++){
        switch(lexer.tokens[index]){
        case Token.LoopEnd:
          if(output.Count == 0) throw new InvalidBracketException("Cannot create an empty loop");
          return new(output);

        case Token.LoopStart:
          output.Add(ParseLoop(lexer, ref index));
          break;

        default:
          output.Add(new Atom(lexer.tokens[index]));
          break;
        }
      }
      throw new Exception("Never");
    }
  }
}
