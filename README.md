# Azure DevOps Pipeline Templating

Experiments with reuse in Azure DevOps (ADO) pipeline templating.

## Background

[endjin.com](https://endjin.com) defines various reusable ADO pipeline templates at https://github.com/endjin/Endjin.RecommendedPractices.AzureDevopsPipelines.GitHub

The repo contains experiments that I'd like to perform without messing up any of those shared templates. This lets me discover which techniques for reuse work, and which do not, prior to making updates to that shared repo.

## Experiment: nesting step templates

It's possible to write an ADO template that defines extensibility points where particular applications of the template can add their own steps.
Is it possible for the steps we plug into those extensibility points to come from other templates?

This motivation originated from a requirement to add benchmarking to our build process (with the long term goal being to detect
performance regressions automatically). We don't want to force this on all projects, and we'd prefer not to have to maintain two versions
of our shared `build.and.release.yaml` template that are identical save for this one step. The obvious way to avoid the problem of
maintaining multiple variations on a single theme is to have one standard build and release template with some placeholders in
which users of the template can supply optional extra steps.

But what if several projects are all going to want to do the same optional extra steps? If we define a set of steps that, say, run a
BenchmarkDotNet benchmark and then publish the results as part of the build artifacts, we'd want to be able to reuse those across
multiple projects.

This is possible, and the templates in this repo show one way to do it.

The reusable template that performs the actual build is [templates/build.and.test.yaml](../master/templates/build.and.test.yaml). This is a very simplified
version of the template of the same name up at https://github.com/endjin/Endjin.RecommendedPractices.AzureDevopsPipelines.GitHub containing
just build and test steps and, critically, a placeholder:

``` yaml
  - ${{ parameters.postTestSteps }}
```

This `${{ ... }}`` syntax denotes a _template expression_. This is an expression that will be expanded at the point at which ADO produces
what the documentation sometimes calls a _plan_. In simple pipelines, the plan is conceptually indistinguishable from your pipeline
definition, but once you start using templates, it becomes an important concept: when the pipeline file you supply ADO with points to
one or more templates, ADO gathers them altogether and produces what you could think of as the actual build definition it's going to
run - what the build definition might have looked like if you weren't using templates. And it's at that point that it expands any
_template expressions_.

In this repo, the pipeline definition I've supplied to ADO is at [build-pipeline.yaml](../master/build-pipeline.yaml). This defines
a single job, but then defers entirely to a template to define the constituent parts of that job:

``` yaml
jobs:
- template: templates/build.test.and.benchmark.yaml
  parameters:
    vmImage: 'ubuntu-latest'
    solution_to_build: $(Endjin_Solution_To_Build)
    benchmarkProjectName: 'TestBenchmark.Benchmark'
```

When ADO comes to generate the plan for this pipeline, it knows it will have to go and fetch the
[templates/build.test.and.benchmark.yaml](../master/templates/build.test.and.benchmark.yaml) template. That template requires us to tell it
certain things, such as the location of the solution file, and the name of the project that contains the benchmarks we'd like
to run as part of this build.

If you're following closely, you'll have noticed that this does _not_ refer to the `build.and.test.yaml` I showed a snippet from
earlier. It does use that, but it does so indirectly. And that's because the goal of the exercise here is to see if we can find
a reusable way to use reusable templates in certain ways. `build.and.test.yaml` is a reusable template that provides a general-purpose
placeholder for additional build steps; but we're using `build.test.and.benchmark.yaml` which is another reusable template that exploits
that placeholder in a particular way, adding in benchmarking support.

But if you look at that template, it just invokes the `build.and.test.yaml` template from earlier, but, critically, the value it supplies
for the `postTestSteps` parameter is *another template*:

``` yaml
- template: build.and.test.yaml
  parameters:
    vmImage: ${{ parameters.vmImage }}
    solution_to_build: ${{ parameters.solution_to_build }}
    postTestSteps:
    - template: benchmark.yaml
      parameters:
        benchmarkProjectName: ${{ parameters.benchmarkProjectName }}
```

And this was the question I was attempting to answer with this experiment: when a template (like `build.and.test.yaml`) defines a
parameter that lets you supply a bunch of extra steps to run at a particular point, can those steps be sourced from some other
template? And the answer is: yes!

This means that I can define by `build.test.and.benchmark.yaml` by saying that it's basically the `build.and.test.yaml` template,
but with another template, [templates/benchmark.yaml](../master/templates/benchmark.yaml), plugged into the placeholder
offered by `build.and.test.yaml`.

## Experiment: calculated default template arguments

In the `benchmark.yaml` template used in the preceding experiment, we want to be able to invoke it with a single parameter, `benchmarkProjectName`, and have it infer the exact project folder and file location from this, but in some situations it may be useful to be able to specify the locations explicitly.

For example, given the project name `TestBenchmark.Benchmark`, the templates presume that the project folder is `Solutions/TestBenchmark.Benchmark` and that this contains a `TestBenchmark.Benchmark.csproj` file. But there may be circumstances in which it's useful to use a different folder structure—the benchmark project might be a subfolder rather than directly inside `Solutions` for example.

To support this, the `benchmark.yaml` defines three arguments: `benchmarkProjectName`, `benchmarkProjectFolder`, and `benchmarkProjectPath`, but if you set just the first one, it calculates values for the second two, enabling us to pass just the `benchmarkProjectName`.

Unfortunately, this is slightly more involved than we might like. Ideally we'd be able to write this sort of thing at the top of the template:

``` yaml
# Will cause an error - ADO doesn't let you use template expressions as parameter defaults
parameters:
  benchmarkProjectName: ${{ parameters.benchmarkProjectName }}
  benchmarkProjectFolder: ${{ coalesce(parameters.benchmarkProjectFolder, format('$(Build.SourcesDirectory)/Solutions/{0}', parameters.benchmarkProjectName)) }}
  benchmarkProjectPath: ${{ coalesce(parameters.benchmarkProjectPath, format('{0}/{1}.csproj', coalesce(parameters.benchmarkProjectFolder, format('$(Build.SourcesDirectory)/Solutions/{0}', parameters.benchmarkProjectName)), parameters.benchmarkProjectName)) }}
```

The `coalesce` function here will use its first parameter unless that's null or an empty string, in which case it will use the next argument. So this will use parameter values if supplied, with calculated fallbacks. Or at least, it would if it worked.

Unfortunately this doesn't work, because ADO doesn't allow you to use template expressions as the default values in a template's `parameters` section. The defaults all have to be constant values.

In a job template, you can define a `variables` section, and since variables _are_ allowed to use template expressions, we can put the expressions we want there, and then refer to the variables elsewhere in the template instead of the parameters. See [templates/build.and.test.yaml](../master/templates/build.and.test.yaml#L11) for an example.

Unfortunately, it seems that you can't declare variables at file scope in a step template. This means there's no way to define a calculated default value for a parameter at file scope. You can declare per-step variables, but that's not much use if you want to use the same value in multiple steps.

To work around this, we split the template into two files. If you look at [templates/benchmark.yaml](../master/templates/benchmark.yaml)
you'll see that it defines no steps. It just calls out to another template, [templates/benchmark-impl.yaml](../master/templates/benchmark-impl.yaml), that does the real work.
The only purpose of `benchmark.yaml` is to plug in calculated default values. It does this at the point where it specifies the parameters to `benchmark-impl.yaml`.

This is a rather ungainly hack, but it does work around the apparent limitation. But life would be much simpler if ADO either allowed template expressions for default values for template parameters, or allowed you to define file-scope variables for step templates.
