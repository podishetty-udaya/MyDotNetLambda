
1️⃣ GitHub Repository Structure
MyDotNetLambda/
├─ src/
│  └─ MyLambdaProject/
│      ├─ MyLambdaProject.csproj
│      └─ Function.cs
├─ buildspec.yml
└─ template.yaml
2️⃣ Lambda Function Code (Function.cs)
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

Simple function: logs input and returns a string.

Targeted for .NET 6/7.

3️⃣ SAM Template (template.yaml)
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: Simple .NET Lambda Example

Resources:
  MyLambdaFunction:
    Type: AWS::Serverless::Function
    Properties:
      FunctionName: MyDotNetLambda
      Handler: MyLambdaProject::MyLambdaProject.Function::Handler
      Runtime: dotnet8
      MemorySize: 128
      Timeout: 10
      CodeUri: src/MyLambdaProject/
      Policies:
        - AWSLambdaBasicExecutionRole

4️⃣ Build Specification (buildspec.yml)
version: 0.2

env:
  variables:
    LAMBDA_FUNCTION_NAME: MyDotNetLambda
    ARTIFACT_BUCKET: my-dotnet-lambda-artifacts
    AWS_REGION: us-east-1
    ASSUME_ROLE_ARN: arn:aws:iam::758742552347:role/CodeBuildDeployRole

phases:
  install:
    commands:
      - echo "Updating system packages and installing dependencies"
      - apt-get update -y
      - apt-get install -y libicu70 libssl3 libkrb5-3 libunwind8 zlib1g wget
      - echo "Installing .NET 8 SDK manually"
      - wget https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.418/dotnet-sdk-8.0.418-linux-x64.tar.gz -O dotnet8.tar.gz
      - mkdir -p /usr/share/dotnet
      - tar -xzf dotnet8.tar.gz -C /usr/share/dotnet
      - export DOTNET_ROOT=/usr/share/dotnet
      - export PATH=$DOTNET_ROOT:$PATH:$HOME/.dotnet/tools
      - export DOTNET_ROLL_FORWARD=LatestMajor
      - echo "Checking for any global.json files"
      - find /codebuild -name global.json
      - rm -f /codebuild/global.json
      - dotnet --version
      - echo "Installing Lambda Tools"
      - dotnet tool install -g Amazon.Lambda.Tools
      - export PATH=$PATH:$HOME/.dotnet/tools
      - dotnet --version

  pre_build:
    commands:
      - echo "Assuming role via STS"
      - CREDS_JSON=$(aws sts assume-role --role-arn $ASSUME_ROLE_ARN --role-session-name CodeBuildSession)
      - export AWS_ACCESS_KEY_ID=$(echo $CREDS_JSON | jq -r '.Credentials.AccessKeyId')
      - export AWS_SECRET_ACCESS_KEY=$(echo $CREDS_JSON | jq -r '.Credentials.SecretAccessKey')
      - export AWS_SESSION_TOKEN=$(echo $CREDS_JSON | jq -r '.Credentials.SessionToken')
      - echo "Temporary STS credentials:"
      - echo "AWS_ACCESS_KEY_ID=$AWS_ACCESS_KEY_ID"
      - echo "AWS_SECRET_ACCESS_KEY=$AWS_SECRET_ACCESS_KEY"
      - echo "AWS_SESSION_TOKEN=$AWS_SESSION_TOKEN"
      - aws sts get-caller-identity

  build:
    commands:
      - echo "Building .NET Lambda project"
      - dotnet build src/MyLambdaProject/MyLambdaProject.csproj -c Release
      - echo "Packaging Lambda"
      - dotnet lambda package -c Release -o MyLambda.zip

  post_build:
    commands:
      - echo "Uploading Lambda zip to S3"
      - aws s3 cp MyLambda.zip s3://$ARTIFACT_BUCKET/MyLambda.zip
      - echo "Deploying Lambda function"
      - aws lambda update-function-code --function-name $LAMBDA_FUNCTION_NAME --s3-bucket $ARTIFACT_BUCKET --s3-key MyLambda.zip

Step 1: Create S3 Bucket for Build Artifacts

Open AWS Console → S3 → Create bucket

Bucket name: my-dotnet-lambda-artifacts (must be globally unique)

Region: choose your preferred region (e.g., us-east-1)

Bucket settings: leave defaults (uncheck “Block all public access”)

Click Create bucket

✅ This bucket will store CodeBuild artifacts (zipped Lambda packages).

Step 2: Create Lambda Execution Role

Lambda needs a role to run and log:

Open AWS Console → IAM → Roles → Create role

Trusted entity: AWS service → Lambda → Next

Permissions:

Attach AWSLambdaBasicExecutionRole

This allows writing logs to CloudWatch

(Optional) Add additional permissions if Lambda needs S3 access, RDS, etc.

Role name: LambdaExecutionRole

Click Create role

✅ This role will be assigned to your Lambda in template.yaml:

Policies:
  - AWSLambdaBasicExecutionRole
Step 3: Create CodeBuild Service Role

CodeBuild needs a service role to:

Assume roles (STS)

Upload artifacts to S3

Deploy Lambda

Write logs to CloudWatch

Steps:

Open AWS Console → IAM → Roles → Create role

Trusted entity: AWS service → CodeBuild → Next

Permissions:

AmazonS3FullAccess → upload artifact to S3

AWSLambdaFullAccess → deploy Lambda

CloudWatchLogsFullAccess → write build logs

STS AssumeRole → allow assuming any other role if needed

Click Next, give it Role name: CodeBuildDeployRole

Click Create role

Step 4: Attach Role to CodeBuild Project

Go to AWS Console → CodeBuild → Create project (or edit existing)

In Environment → Service role: choose CodeBuildDeployRole

Save the project

✅ CodeBuild will now assume this role to:

Upload to S3

Deploy Lambda

Log to CloudWatch

Step 5: Update buildspec.yml to Use STS (Optional)

If you want to explicitly assume the CodeBuildDeployRole (simulates temp credentials flow):

pre_build:
  commands:
    - echo "Assuming CodeBuild role via STS"
    - CREDS_JSON=$(aws sts assume-role --role-arn arn:aws:iam::<ACCOUNT_ID>:role/CodeBuildDeployRole --role-session-name CodeBuildSession)
    - export AWS_ACCESS_KEY_ID=$(echo $CREDS_JSON | jq -r '.Credentials.AccessKeyId')
    - export AWS_SECRET_ACCESS_KEY=$(echo $CREDS_JSON | jq -r '.Credentials.SecretAccessKey')
    - export AWS_SESSION_TOKEN=$(echo $CREDS_JSON | jq -r '.Credentials.SessionToken')
    - aws sts get-caller-identity
Step 6: Assign Lambda Role in template.yaml
Resources:
  MyLambdaFunction:
    Type: AWS::Serverless::Function
    Properties:
      FunctionName: MyDotNetLambda
      Handler: MyLambdaProject::MyLambdaProject.Function::Handler
      Runtime: dotnet8
      MemorySize: 128
      Timeout: 10
      CodeUri: src/MyLambdaProject/
      Role: arn:aws:iam::<ACCOUNT_ID>:role/LambdaExecutionRole

Lambda uses basic execution role to run and log

CodeBuild uses service role to deploy via pipeline

✅ Summary
Resource	Purpose	Steps
S3 Bucket	Store build artifacts	Create bucket → unique name → note bucket name
Lambda Execution Role	Lambda runs & logs	IAM → Create role → AWSLambdaBasicExecutionRole → attach to Lambda
CodeBuild Service Role	Pipeline builds & deploys	IAM → Create role → S3 + Lambda + CloudWatch + STS permissions → attach to CodeBuild

This setup ensures:

Roles are least-privilege but functional

S3 stores artifacts safely

STS temporary credentials are used when CodeBuild assumes roles

Lambda can run safely with logs


Absolutely! Let’s go step by step for creating a full CI/CD pipeline using CodeStar / CodePipeline for your .NET 8 Lambda. I’ll keep it practical and beginner-friendly.

Step 0: Prerequisites

GitHub repository with your Lambda project and buildspec.yml

MyDotNetLambda/
├─ src/MyLambdaProject/Function.cs
├─ src/MyLambdaProject/MyLambdaProject.csproj
├─ buildspec.yml
└─ template.yaml

S3 bucket to store build artifacts (e.g., my-dotnet-lambda-artifacts)

IAM roles:

CodeBuild service role → can assume role, access S3, update Lambda, log to CloudWatch

Lambda execution role → basic Lambda permissions

Step 1: Create CodeStar Connection (GitHub → AWS)

Go to AWS Console → CodeStar connections

Click Create connection

Select GitHub

Authenticate & authorize AWS to access your GitHub repo

Give it a name like MyGitHubConnection

This allows CodePipeline to automatically pull code from GitHub.

Step 2: Create CodeBuild Project

Go to AWS Console → CodeBuild → Create project

Project name: MyDotNetLambdaBuild

Source: GitHub → select the repository & branch

Connection: choose the CodeStar connection you created

Environment:

Managed image → Ubuntu 22.04 / Standard

Runtime: .NET 8

Buildspec: use buildspec.yml from the repo

Service role:

Choose existing IAM role (with S3, Lambda, CloudWatch, STS assume-role permissions)

Save project

Step 3: Create CodePipeline

Go to AWS Console → CodePipeline → Create pipeline

Pipeline name: MyDotNetLambdaPipeline

Service role: let AWS create one or choose your own

Advanced: Enable artifact encryption (optional)

Add Pipeline Stages
Stage 1: Source

Provider: GitHub

Connection: CodeStar connection

Repository & branch: select your repo & main branch

Output artifact: SourceOutput

Stage 2: Build

Provider: AWS CodeBuild

Project name: select MyDotNetLambdaBuild

Input artifact: SourceOutput

Output artifact: BuildOutput

Stage 3: Deploy

Provider: AWS CloudFormation or AWS Lambda Deploy

Input artifact: BuildOutput

Stack name: MyDotNetLambdaStack

Capabilities: CAPABILITY_IAM (SAM / Lambda needs this)

Step 4: Pipeline Flow Overview
GitHub (via CodeStar) → Source Stage
       ↓
CodeBuild (assumes role via STS) → Build & Package
       ↓
S3 Artifact Bucket → CloudFormation / Lambda Deploy
       ↓
Lambda Updated → Logs in CloudWatch
Step 5: Test Pipeline

Push a commit to GitHub branch

Pipeline triggers automatically

Stages:

Source: fetch code

Build: run buildspec → compile, package, STS temp creds exported, upload artifact

Deploy: update Lambda

✅ You’ll see STS temporary credentials used by CodeBuild (if your buildspec echoes them)

Step 6: Verify Deployment

Open AWS Lambda → MyDotNetLambda

Test function manually or trigger via API Gateway (if you added it)

Check CloudWatch Logs for successful execution

Step 7: Optional Enhancements

Add Unit Tests in buildspec before packaging

Add approval stage in pipeline for production deployments

Use parameterized SAM template for multiple environments

Key Points

CodeStar connection → GitHub integration for source stage

CodeBuild role → assumes role via STS (temporary credentials) for S3, Lambda, CloudWatch

Pipeline stages → Source → Build → Deploy

Artifact bucket → stores packaged Lambda

Logs confirm pipeline runs and STS creds are valid

I can also draw a simple diagram showing GitHub → CodeStar → CodeBuild → STS → Lambda → CloudWatch for interview-ready flow if you want.