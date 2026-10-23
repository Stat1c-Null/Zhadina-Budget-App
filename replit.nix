{ pkgs }: {
  deps = [
    pkgs.dotnetCorePackages.sdk_10_0
    pkgs.icu
  ];
}
