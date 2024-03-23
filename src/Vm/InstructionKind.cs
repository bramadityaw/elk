namespace Elk.Vm;

enum InstructionKind : byte
{
    Nop,
    Load,
    Store,
    LoadUpper,
    StoreUpper,
    Pop,
    PopArgs,
    Unpack,
    UnpackUpper,
    ExitBlock,
    Ret,
    Call,
    RootCall,
    MaybeRootCall,
    CallStd,
    CallProgram,
    RootCallProgram,
    MaybeRootCallProgram,
    /// <summary>
    /// `ResolveArgumentsDynamically` should be used before this
    /// </summary>
    DynamicCall,
    PushArgsToRef,
    PushClosureToRef,
    ResolveArgumentsDynamically,

    Index,
    IndexStore,
    New,
    BuildTuple,
    BuildList,
    BuildListBig,
    BuildSet,
    BuildDict,
    BuildRange,
    BuildString,
    Const,
    StructConst,
    Glob,

    Add,
    Sub,
    Mul,
    Div,
    Mod,
    Negate,
    Not,
    Equal,
    NotEqual,
    Greater,
    GreaterEqual,
    Less,
    LessEqual,
    And,
    Or,
    Contains,

    Jump,
    JumpBackward,
    JumpIf,
    JumpIfNot,
    PopJumpIf,
    PopJumpIfNot,
    GetIter,
    ForIter,
    EndFor,
}