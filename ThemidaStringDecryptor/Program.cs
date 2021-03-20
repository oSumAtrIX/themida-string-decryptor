using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using System;
using System.Linq;
using System.Text;

namespace ThemidaStringDecryptor
{
    internal class Program
    {
        private static int ResolveArithmethics(CilCode code, int a, int b)
        {
            return code switch
            {
                CilCode.And => a & b,
                CilCode.Or => a | b,
                _ => a ^ b,
            };
        }
        static void Main(string[] args)
        {
            ModuleDefinition module = ModuleDefinition.FromFile(args[0]);
            System.Collections.Generic.IEnumerable<TypeDefinition> types = module.GetAllTypes();
            TypeDefinition stringDecryptType = types.FirstOrDefault(x => x.FullName.Contains("PrivateImpl"));
         
            int resolveStringMethod = stringDecryptType.Methods.FirstOrDefault(x => x.Parameters.Count == 3).MetadataToken.ToInt32();

            CilInstructionCollection stringDecryptTypeCtorInstructions = stringDecryptType.GetStaticConstructor().CilMethodBody.Instructions;

            byte[] decryptArray = ((DataSegment)((FieldDefinition)stringDecryptTypeCtorInstructions[6].Operand).FieldRva).ToArray();

            var firsArithmetic = stringDecryptTypeCtorInstructions[18].OpCode.Code;
            var secondNumber = stringDecryptTypeCtorInstructions[19].GetLdcI4Constant();
            var secondArithmetic = stringDecryptTypeCtorInstructions[20].OpCode.Code;

            for (int i = 0; i < decryptArray.Length; i++)
            {
                decryptArray[i] = (byte)(ResolveArithmethics(secondArithmetic , ResolveArithmethics(firsArithmetic, decryptArray[i], i), secondNumber) );
			}

            foreach (var moduleMethods in types.SelectMany(type => type.Methods.Where(md => md.HasMethodBody)))
            {
                for (int i = 0; i < moduleMethods.CilMethodBody.Instructions.Count; i++)
                {
                    CilInstruction methodInstructions = moduleMethods.CilMethodBody.Instructions[i];
                    if (methodInstructions.OpCode.Code != CilCode.Call  || methodInstructions.Operand is not MethodDefinition)
                        continue;

                    MethodDefinition proxyMethod = (MethodDefinition)methodInstructions.Operand;
                    if (proxyMethod.Signature.ReturnType.ElementType != ElementType.String)
                        continue;

                    CilInstructionCollection proxyMethodInstructions = (proxyMethod).CilMethodBody.Instructions;
                    if (proxyMethodInstructions.Count != 11 || ((MethodDefinition)proxyMethodInstructions[9].Operand).MetadataToken.ToInt32() != resolveStringMethod)
                        continue;

                    moduleMethods.CilMethodBody.Instructions[i] = new CilInstruction(CilOpCodes.Ldstr, Encoding.UTF8.GetString(decryptArray,  proxyMethodInstructions[7].GetLdcI4Constant(), proxyMethodInstructions[8].GetLdcI4Constant()));
                    Console.WriteLine(moduleMethods.CilMethodBody.Instructions[i]);
                }
            }
            new ManagedPEFileBuilder().CreateFile(new ManagedPEImageBuilder().CreateImage(module).ConstructedImage).Write("out.exe");
        }

    }
}
