const path = require('path');
const pkg = require('./package.json');
const nodeExternals = require('webpack-node-externals');
module.exports = {
    entry: "./index.js",
    output: {
      path: path.resolve(__dirname, 'dist'),
      filename: "retrodryreact.js",
      library: pkg.name,
      libraryTarget: "commonjs2"
    },
    target: 'node',
    externals: [nodeExternals()],
    module: {
      rules: [
        {
          test: /\.(js|jsx)$/,
          exclude: /node_modules/,
          use: {
            loader: "babel-loader"
          }
        }
      ]
    }
};