using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyLambdaProject
{
    public class Function
    {
        public string Handler(string input, ILambdaContext context)
        {
            context.Logger.LogLine($"Received input: {input}");
            return $"Hello from Lambda! You sent: {input}";
        }
    }
}

